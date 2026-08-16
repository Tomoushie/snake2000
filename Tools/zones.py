import re
from collections import Counter

SEP = chr(92)
GAME = SEP + "Game" + SEP
ENGINE = SEP + "Engine" + SEP

par = Counter()
restants = []
for l in open("diag_erreurs.txt", encoding="utf-8"):
    if "error CS" not in l:
        continue
    chemin = l.split("(")[0]
    if GAME in chemin:
        zone = "Game/"
        restants.append(l)
    elif ENGINE in chemin:
        zone = "Engine/"
    else:
        zone = "autre"
    par[zone] += 1

print("=== erreurs par zone ===")
for z, n in par.most_common():
    print(f"  {n:5}  {z}")
print()
print(f"=== les {len(restants)} erreurs restantes de Game/ ===")
for l in restants:
    f = l.split("(")[0].split(SEP)[-1]
    m = re.search(r"(error CS\d+: .{0,70})", l)
    print(f"  {f:26} {m.group(1) if m else ''}")
