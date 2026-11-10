using System.Runtime.InteropServices;
using Gitunia.Core.Updates;
using Gitunia.Platform.Updates;

namespace Gitunia.App.Updates;

/// <summary>
/// <c>gitunia update [--check]</c>: compares the running version with the latest release and, for an install a script made,
/// replaces the binary after checking the download's checksum and running the new binary with <c>--version</c>. A package
/// manager's install gets its command printed instead; nothing it owns is touched. Writes to the given writer only.
/// </summary>
public sealed class UpdateCommand(ReleaseClient client, ExecutableSwapper swapper, TextWriter output)
{
    private const string ChecksumSuffix = ".sha256";
    private const string WorkFolderPrefix = "gitunia-update-";
    private const string BinaryFileName = "new-binary";

    /// <summary>Runs the command and returns its exit code (<see cref="UpdateExitCode"/>). Never throws but for cancellation.</summary>
    public async Task<int> RunAsync(UpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ReleaseVersion.TryParse(request.CurrentVersion, out var current) || current.IsDevelopmentBuild)
        {
            await output.WriteLineAsync($"gitunia update: this is a development build ({request.CurrentVersion}), not a release.");

            return UpdateExitCode.Failed;
        }

        ReleaseInfo latest;
        try
        {
            latest = await client.GetLatestAsync(cancellationToken);
        }
        catch (UpdateException exception)
        {
            await output.WriteLineAsync($"gitunia update: {exception.Message}");
            await output.WriteLineAsync($"Releases: {UpdateAdvice.ReleasesUrl}");

            return UpdateExitCode.Failed;
        }

        if (!latest.Version.IsNewerThan(current))
        {
            await output.WriteLineAsync($"Gitunia {current} is up to date.");

            return UpdateExitCode.Success;
        }

        await output.WriteLineAsync($"Gitunia {latest.Version} is available (you have {current}).");
        if (request.CheckOnly)
        {
            return UpdateExitCode.UpdateAvailable;
        }

        var route = request.Facts is null ? InstallRoute.Unknown : InstallRouteDetector.Detect(request.Facts);
        if (route != InstallRoute.Script || request.Facts is null)
        {
            await ExplainManualUpdateAsync(route, request, latest);

            return UpdateExitCode.Success;
        }

        return await ReplaceAsync(request.Facts, request.Architecture, current, latest, cancellationToken);
    }

    private async Task ExplainManualUpdateAsync(InstallRoute route, UpdateRequest request, ReleaseInfo latest)
    {
        var command = UpdateAdvice.CommandFor(route);
        if (command is not null)
        {
            await output.WriteLineAsync($"This copy is managed by your package manager ({route}); update it with:");
            await output.WriteLineAsync($"  {command}");

            return;
        }

        await output.WriteLineAsync("This copy was not installed by an install script or a package manager, so it is not replaced.");
        var system = request.Facts?.System ?? InstallFactsReader.CurrentSystem();
        var suffix = ReleaseTarget.ArchiveSuffix(system, request.Architecture);
        var asset = suffix is null ? null : latest.FindBySuffix(suffix);
        var download = asset is null ? UpdateAdvice.ReleasesUrl : asset.Url.ToString();
        await output.WriteLineAsync($"Download it, unpack it and replace gitunia yourself: {download}");
    }

    private async Task<int> ReplaceAsync(
        InstallFacts facts,
        Architecture architecture,
        ReleaseVersion current,
        ReleaseInfo latest,
        CancellationToken cancellationToken)
    {
        var suffix = ReleaseTarget.ArchiveSuffix(facts.System, architecture);
        var asset = suffix is null ? null : latest.FindBySuffix(suffix);
        if (asset is null)
        {
            await output.WriteLineAsync($"gitunia update: release {latest.Version} has no archive for this system.");
            await output.WriteLineAsync($"Releases: {UpdateAdvice.ReleasesUrl}");

            return UpdateExitCode.Failed;
        }

        await output.WriteLineAsync($"Updating {facts.ExecutablePath} from {asset.Url}");
        var work = Path.Combine(Path.GetTempPath(), $"{WorkFolderPrefix}{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(work);
            await DownloadAndSwapAsync(facts, asset, latest, work, cancellationToken);
        }
        catch (Exception exception) when (exception is UpdateException or IOException or UnauthorizedAccessException)
        {
            await output.WriteLineAsync($"gitunia update: {exception.Message}");
            if (exception is UnauthorizedAccessException)
            {
                var folder = Path.GetDirectoryName(facts.ExecutablePath);
                await output.WriteLineAsync(
                    $"Gitunia cannot write to {folder}: run the update with the rights that folder needs, or use the install script.");
            }

            await output.WriteLineAsync($"Nothing was changed. Do it by hand: download {asset.Url}, unpack it and replace {facts.ExecutablePath}.");

            return UpdateExitCode.Failed;
        }
        finally
        {
            DeleteWorkFolder(work);
        }

        await output.WriteLineAsync($"Updated Gitunia {current} -> {latest.Version}.");

        return UpdateExitCode.Success;
    }

    private async Task DownloadAndSwapAsync(
        InstallFacts facts,
        ReleaseAsset asset,
        ReleaseInfo latest,
        string work,
        CancellationToken cancellationToken)
    {
        var expected = latest.Version;
        var checksum = FindChecksum(asset, latest);
        var archivePath = Path.Combine(work, asset.Name);
        await client.DownloadFileAsync(asset.Url, archivePath, cancellationToken);
        var checksumText = await client.DownloadTextAsync(checksum, cancellationToken);
        ReleaseArchive.VerifyChecksum(archivePath, checksumText);

        var newBinary = Path.Combine(work, BinaryFileName + Path.GetExtension(facts.ExecutablePath));
        ReleaseArchive.ExtractBinary(archivePath, ReleaseTarget.BinaryName(facts.System), newBinary);
        var reported = await BinaryProbe.ReadVersionAsync(newBinary, cancellationToken);
        if (reported is null || reported.CompareTo(expected) != 0)
        {
            throw new UpdateException(
                $"the downloaded binary did not report version {expected} ({reported?.ToString() ?? "it did not run"})");
        }

        swapper.Replace(facts.ExecutablePath, newBinary);
    }

    // The release lists the checksum as its own asset (the archive's name plus .sha256).
    private static Uri FindChecksum(ReleaseAsset asset, ReleaseInfo latest)
    {
        var checksum = latest.FindByName($"{asset.Name}{ChecksumSuffix}");
        if (checksum is null)
        {
            throw new UpdateException($"the release has no checksum file for {asset.Name}");
        }

        return checksum.Url;
    }

    private static void DeleteWorkFolder(string work)
    {
        try
        {
            Directory.Delete(work, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary folder is harmless; the operating system clears it.
        }
    }
}
