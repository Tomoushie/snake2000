"""Detecte les types declares deux fois dans le MEME espace de noms."""
import os
import re
from collections import defaultdict

RACINE = r"E:\Corpus\Snake2000"
NS = re.compile(r"^\s*namespace\s+([A-Za-z0-9_.]+)", re.M)
TYPE = re.compile(r"^\s*(?:public|internal)\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+)*"
                  r"(class|interface|struct|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)", re.M)

table = defaultdict(list)
partiels = set()

for dossier in ("AI", "Engine", "Game", "Systems"):
    for base, _, fichiers in os.walk(os.path.join(RACINE, dossier)):
        for nom in fichiers:
            if not nom.endswith(".cs"):
                continue
            plein = os.path.join(base, nom)
            rel = os.path.relpath(plein, RACINE).replace(os.sep, "/")
            try:
                txt = open(plein, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            m = NS.search(txt)
            espace = m.group(1) if m else "<global>"
            for ligne in txt.splitlines():
                t = TYPE.match(ligne)
                if t:
                    cle = (espace, t.group(2))
                    table[cle].append(rel)
                    if "partial" in ligne:
                        partiels.add(cle)

conflits = {k: v for k, v in table.items()
            if len(set(v)) > 1 and k not in partiels}

print(f"types declares       : {len(table)}")
print(f"conflits CS0101 reels: {len(conflits)}")
print()
for (espace, nom), fichiers in sorted(conflits.items()):
    print(f"{espace}.{nom}")
    for f in sorted(set(fichiers)):
        print(f"    {f}")
