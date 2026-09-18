"""Check every Harmony patch in BiomeLords against a decompiled Valheim assembly.

The C# compiler cannot see a Harmony target: the class and member are strings (or
nameof, which only checks the name exists *somewhere* on the type). So a Valheim
update that renames a parameter, adds an overload, or changes a signature compiles
cleanly and fails at load time — or worse, at first use.

This script catches the two failure modes we have actually hit, offline:

  1. TARGET RESOLUTION  — does the type.member named in each [HarmonyPatch] and each
     AccessTools.*(typeof(T), "name") still exist?  (skipped patch classes:
     "Could not find method", "Ambiguous match")
  2. PARAMETER BINDING  — Harmony binds Prefix/Postfix parameters to the target's
     parameters BY NAME. A renamed vanilla parameter only fails at runtime with
     'Parameter "x" not found' → "IL Compile Error".  Checks every injected name
     against the decompiled signature.

It does NOT catch a changed parameter *type* on an explicit `new[] { typeof(...) }`
array — for those, read the Harmony warning in LogOutput.log or grep the decompile.
It also cannot see MissingMethodException from a call site whose target gained an
optional parameter; those are fixed simply by rebuilding against the new assembly.

Usage (from the project root, after decompiling — see development.md):

    python Docs/tools/verify_patch_targets.py --decompiled "<dir of ilspycmd -p output>"

Exit code 1 if anything is unresolved, so it can gate a build.

Heuristic caveats: it matches by name, treats nested types (ItemDrop.ItemData) as
living in the outer type's file, and attributes a Prefix/Postfix to the nearest
[HarmonyPatch(typeof(...), "...")] above it. Classes that pick their target in
TargetMethod()/TargetMethods() under a bare [HarmonyPatch] are skipped by the
parameter check, since their target can't be read statically.
"""
import argparse
import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_SRC = os.path.abspath(os.path.join(HERE, "..", ".."))

SKIP_DIRS = ("bin", "obj", ".git", "Docs")

DECL = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|private|protected|internal|static|virtual|override|sealed|abstract|extern|unsafe|new|async|\s)+"
    r"[\w<>,\[\]\.\?]+\s+(\w+)\s*\(([^)]*)\)\s*(?:\{|$)", re.M)

SPECIAL = re.compile(r"^(__instance|__result|__state|__originalMethod|__args|__runOriginal|___.*)$")


def index_vanilla(decompiled):
    """type -> {member names};  type -> {method -> {param names}}"""
    members, params = {}, {}
    for fn in os.listdir(decompiled):
        if not fn.endswith(".cs"):
            continue
        t = fn[:-3]
        body = io.open(os.path.join(decompiled, fn), encoding="utf-8", errors="replace").read()
        names = set(re.findall(r"\b([A-Za-z_]\w*)\s*\(", body))
        names |= set(re.findall(
            r"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|static|readonly|const|new|override|virtual|sealed|extern|unsafe|volatile|\s)+[\w<>,\[\]\.\?]+\s+(\w+)\s*[;=]",
            body, re.M))
        names |= set(re.findall(r"\b(\w+)\s*\{\s*(?:get|set)", body))
        members[t] = names

        d = params.setdefault(t, {})
        for m in DECL.finditer(body):
            name, arglist = m.group(1), m.group(2)
            pn = set()
            for a in arglist.split(","):
                a = a.strip().split("=")[0].strip()
                tok = a.split()
                if tok:
                    pn.add(tok[-1])
            d.setdefault(name, set()).update(pn)
    return members, params


def outer(t):
    return t.split(".")[0]


def walk_sources(src):
    for root, dirs, files in os.walk(src):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for fn in files:
            if fn.endswith(".cs"):
                yield os.path.join(root, fn)


def check_targets(src, members):
    problems, checked = [], 0
    for path in walk_sources(src):
        rel = os.path.relpath(path, src)
        text = io.open(path, encoding="utf-8", errors="replace").read()
        refs = []
        refs += [(m.group(1), m.group(2), m.start()) for m in
                 re.finditer(r'HarmonyPatch\(\s*typeof\(([\w\.]+)\)\s*,\s*"([^"]+)"', text)]
        refs += [(m.group(1), m.group(3), m.start()) for m in
                 re.finditer(r'HarmonyPatch\(\s*typeof\(([\w\.]+)\)\s*,\s*nameof\(([\w\.]+)\.(\w+)\)', text)]
        refs += [(m.group(2), m.group(3), m.start()) for m in
                 re.finditer(r'AccessTools\.(\w+)\(\s*typeof\(([\w\.]+)\)\s*,\s*"([^"]+)"', text)]
        for t, m, off in refs:
            if outer(t) not in members:
                continue  # our own type, Jotunn, Unity — not vanilla
            checked += 1
            if m not in members[outer(t)]:
                problems.append((rel, text.count("\n", 0, off) + 1, t, m))
    return checked, problems


def check_params(src, params):
    problems, checked = [], 0
    for path in walk_sources(src):
        rel = os.path.relpath(path, src)
        text = io.open(path, encoding="utf-8", errors="replace").read()
        cur = None
        for ln, line in enumerate(text.split("\n"), 1):
            m = re.search(r'HarmonyPatch\(\s*typeof\(([\w\.]+)\)\s*,\s*(?:"([^"]+)"|nameof\([\w\.]+\.(\w+)\))', line)
            if m:
                cur = (m.group(1), m.group(2) or m.group(3))
                continue
            # A bare [HarmonyPatch] means the class picks its target in TargetMethod(s)();
            # stop attributing its Prefix/Postfix to whatever attribute came before it.
            if re.search(r"\[HarmonyPatch\]", line):
                cur = None
                continue
            pm = re.search(r"static\s+[\w<>,\[\]\.\?]+\s+(Prefix|Postfix)\s*\(([^)]*)\)", line)
            if not pm or cur is None:
                continue
            t, meth = cur
            if outer(t) not in params or meth not in params[outer(t)]:
                continue
            allowed = params[outer(t)][meth]
            for a in pm.group(2).split(","):
                a = a.strip().split("=")[0].strip()
                tok = a.split()
                if not tok:
                    continue
                pname = tok[-1]
                if SPECIAL.match(pname):
                    continue
                checked += 1
                if pname not in allowed:
                    problems.append((rel, ln, t, meth, pname, sorted(allowed)))
    return checked, problems


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--decompiled", required=True, help="directory produced by `ilspycmd -p -o <dir> assembly_valheim.dll`")
    ap.add_argument("--src", default=DEFAULT_SRC, help="BiomeLords project root (default: two levels above this script)")
    args = ap.parse_args()

    if not os.path.isdir(args.decompiled):
        sys.exit("decompiled dir not found: " + args.decompiled)

    members, params = index_vanilla(args.decompiled)
    print("indexed %d vanilla types from %s" % (len(members), args.decompiled))

    n, bad = check_targets(args.src, members)
    print("\n[1] target resolution: checked %d vanilla references" % n)
    for rel, ln, t, m in bad:
        print("    UNRESOLVED  %s:%d  %s.%s" % (rel, ln, t, m))
    if not bad:
        print("    all resolved")

    n2, bad2 = check_params(args.src, params)
    print("\n[2] parameter binding: checked %d injected parameter names" % n2)
    for rel, ln, t, m, p, allowed in bad2:
        print("    MISMATCH    %s:%d  %s.%s injects '%s' -- vanilla has %s" % (rel, ln, t, m, p, allowed))
    if not bad2:
        print("    all names match")

    sys.exit(1 if (bad or bad2) else 0)


if __name__ == "__main__":
    main()
