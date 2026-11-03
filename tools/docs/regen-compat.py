"""Regenerates the four generated compatibility tables from the receivers' Declare(...) notes.

Usage: python tools/docs/regen-compat.py
Runs `WoG.ErmTool compat [--era] [--lang ru]` (built, --no-build) and writes its output byte for byte into
Compatibility/ERM_Compatibility[_ERA][.ru].md — a shell redirect may re-encode the text (PowerShell 5.1 does).
Needs the .NET 8 SDK: DOTNET_ROOT, or %USERPROFILE%/.dotnet8 on the user's machine.
"""
import os
import subprocess
import sys

repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
env = dict(os.environ)
local = os.path.join(os.path.expanduser("~"), ".dotnet8")
if "DOTNET_ROOT" not in env and os.path.isdir(local):
    env["DOTNET_ROOT"] = local
    env["PATH"] = local + os.pathsep + env.get("PATH", "")
env["DOTNET_CLI_UI_LANGUAGE"] = "en"

for era in (False, True):
    for ru in (False, True):
        args = ["dotnet", "run", "--project", os.path.join(repo, "tools", "WoG.ErmTool"), "--no-build", "--", "compat"]
        if era:
            args.append("--era")
        if ru:
            args += ["--lang", "ru"]
        out = subprocess.run(args, capture_output=True, env=env, cwd=repo)
        if out.returncode != 0:
            sys.exit(out.stderr.decode("utf-8", "replace"))
        name = "ERM_Compatibility" + ("_ERA" if era else "") + (".ru" if ru else "") + ".md"
        with open(os.path.join(repo, "Compatibility", name), "wb") as f:
            f.write(out.stdout)
        print("written", name)
