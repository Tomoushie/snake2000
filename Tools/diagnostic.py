"""Compile les dossiers demandes ensemble, en ne gardant que les fichiers sains."""
import os
import re
import subprocess
import sys
from collections import defaultdict

sys.path.insert(0, r"E:\Corpus\OrchestratorAgent")
from file_manager import valider_code

RACINE = r"E:\Corpus\Snake2000"
SCRATCH = os.path.dirname(os.path.abspath(__file__))
CSC = r"C:\Program Files\dotnet\sdk\10.0.303\Roslyn\bincore\csc.exe"
REFS = [
    r"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.30\ref\net8.0",
    r"C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\8.0.30\ref\net8.0",
]

dossiers = sys.argv[1:] or ["Engine", "Game"]

retenus = []
for dossier in dossiers:
    for base, _, fichiers in os.walk(os.path.join(RACINE, dossier)):
        for nom in sorted(fichiers):
            if not nom.endswith(".cs"):
                continue
            plein = os.path.join(base, nom)
            rel = os.path.relpath(plein, RACINE).replace(os.sep, "/")
            contenu = open(plein, encoding="utf-8", errors="replace").read()
            if valider_code(rel, contenu)[0]:
                retenus.append(plein)

print(f"dossiers : {', '.join(dossiers)}")
print(f"fichiers retenus : {len(retenus)}")

rsp = os.path.join(SCRATCH, "diag.rsp")
with open(rsp, "w", encoding="utf-8") as f:
    f.write("-nologo\n-t:library\n")
    f.write(f'-out:"{os.path.join(SCRATCH, "diag.dll")}"\n')
    for dossier in REFS:
        for dll in sorted(os.listdir(dossier)):
            if dll.endswith(".dll"):
                f.write(f'-r:"{os.path.join(dossier, dll)}"\n')
    for plein in retenus:
        f.write(f'"{plein}"\n')

res = subprocess.run([CSC, f"@{rsp}"], capture_output=True, text=True, errors="replace")
sortie = res.stdout + res.stderr
open(os.path.join(SCRATCH, "diag_erreurs.txt"), "w", encoding="utf-8").write(sortie)

compte = defaultdict(int)
par_fichier = defaultdict(int)
for ligne in sortie.splitlines():
    m = re.search(r"error (CS\d+)", ligne)
    if m:
        compte[m.group(1)] += 1
        par_fichier[ligne.split("(")[0].split("\\")[-1]] += 1

total = sum(compte.values())
print(f"erreurs totales : {total}")
print()
print("=== par code (top 12) ===")
for c, n in sorted(compte.items(), key=lambda x: -x[1])[:12]:
    print(f"   {c} x{n}")
print()
print("=== fichiers les plus touches (top 10) ===")
for f, n in sorted(par_fichier.items(), key=lambda x: -x[1])[:10]:
    print(f"   {n:5} {f}")
