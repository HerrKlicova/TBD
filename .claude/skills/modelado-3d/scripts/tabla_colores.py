"""Tabla de colores por celda de la paleta (vértices leídos de los UV) entre la entrega anterior y la nueva; falla si cambia algo que no se esperaba."""
import json, sys; from collections import Counter
d = json.load(open(sys.argv[1])); pares = json.loads(sys.argv[2]); esperado = set(tuple(x) for x in json.loads(sys.argv[3]))
PAPEL = {"88885c": "lateral (casco)", "50541c": "bajos del casco", "b8b8b8": "techo / canto del parapeto", "845c28": "carga (antes)", "b4a080": "carga (ahora)",
         "605c48": "piso, chapas, puertas", "48403c": "bastidor, ruedas, metal", "847c34": "sacos / contenedor", "1c200c": "contorno oscuro", "181c30": "aspilleras / sombra",
         "303424": "bastidor oscuro", "60546c": "ventanillas", "f0dc9c": "luz de ventanillas", "442c0c": "(banda de la horda)"}
ok = True; filas = []
for nombre, a, b in pares:
    A, B = d[a], d[b]; ca = Counter(c for k in A for c in A[k]); cb = Counter(c for k in B for c in B[k])
    cambios = Counter()
    for k in A:
        for x, y in zip(A[k], B.get(k, [])):
            if x != y: cambios[(x, y)] += 1
    malo = [c for c in cambios if c not in esperado]
    if malo: ok = False
    filas.append("\n**%s** · cambios: %s%s\n\n| Celda | Papel | Antes | Ahora |\n|---|---|---|---|" % (nombre, ", ".join("%s → %s: %d" % (x, y, n) for (x, y), n in cambios.items()) or "ninguno",
                 "" if not malo else " · **INESPERADO**"))
    for c in sorted(set(ca) | set(cb), key=lambda c: -max(ca[c], cb[c])):
        filas.append("| %s | %s | %d | %d |" % (c, PAPEL.get(c, ""), ca[c], cb[c]))
print("\n".join(filas)); sys.exit(0 if ok else 1)
