#!/usr/bin/env python3
"""Count constructor parameters in src/ (M7 impact report, #1055).

Usage: scripts/ctor-params.py <tree-root> [min-params]
Finds primary constructors (class/struct/record declarations with a parameter list) and explicit
constructors (a modifier, then a name equal to the enclosing type, then a parameter list). Records
with a primary constructor count their positional parameters. A regex scan, not a parser: comments and
strings are stripped first, generic constraints and nested types are handled well enough for a count.
Prints `params file:line Type` for every constructor with at least min-params (default 6), then totals.
"""
import os, re, sys

root = sys.argv[1] if len(sys.argv) > 1 else "."
minp = int(sys.argv[2]) if len(sys.argv) > 2 else 6

def strip(s):
    s = re.sub(r'/\*.*?\*/', lambda m: re.sub(r'[^\n]', ' ', m.group(0)), s, flags=re.S)
    s = re.sub(r'//[^\n]*', '', s)
    s = re.sub(r'"(?:\\.|[^"\\\n])*"', '""', s)
    return s

def split_params(text):
    depth = 0; n = 0; cur = ""
    for ch in text:
        if ch in "<([{": depth += 1
        elif ch in ">)]}": depth -= 1
        if ch == "," and depth == 0:
            n += 1 if cur.strip() else 0; cur = ""
        else:
            cur += ch
    if cur.strip(): n += 1
    return n

def balanced(s, i):
    depth = 0
    for j in range(i, len(s)):
        if s[j] == "(": depth += 1
        elif s[j] == ")":
            depth -= 1
            if depth == 0: return j
    return -1

type_re = re.compile(r'\b(class|struct|record(?:\s+(?:class|struct))?|interface)\s+(\w+)(?:<[^>{(]*>)?\s*(\()?')
ctor_re = re.compile(r'\b(?:public|internal|private|protected)\s+(?:static\s+)?(\w+)\s*\(')

results = []
for d, _, files in os.walk(os.path.join(root, "src")):
    if "/obj" in d or "/bin" in d: continue
    for f in files:
        if not f.endswith(".cs"): continue
        p = os.path.join(d, f)
        s = strip(open(p, encoding="utf-8").read())
        kinds = {}
        for m in type_re.finditer(s):
            kind = "class" if m.group(1) in ("class", "interface") else "data"
            kinds[m.group(2)] = kind
            if m.group(3):
                e = balanced(s, m.end() - 1)
                if e > 0:
                    results.append((split_params(s[m.end():e]), p, s.count("\n", 0, m.start()) + 1, m.group(2), kind))
        for m in ctor_re.finditer(s):
            if m.group(1) in kinds:
                e = balanced(s, m.end() - 1)
                if e > 0:
                    results.append((split_params(s[m.end():e]), p, s.count("\n", 0, m.start()) + 1, m.group(1), kinds[m.group(1)]))
results.sort(key=lambda r: -r[0])
for n, p, line, name, kind in results:
    if n >= minp: print(f"{n} {kind} {os.path.relpath(p, root)}:{line} {name}")
over = [r for r in results if r[0] > 5]
print(f"TOTAL_CTORS={len(results)} OVER5={len(over)} OVER5_CLASS={sum(1 for r in over if r[4] == 'class')} OVER5_DATA={sum(1 for r in over if r[4] == 'data')} MAX={results[0][0] if results else 0}")
