# Nules-La Vilavella v0.1: piezas nuevas · Teo, 8-nov

Para Iker. Siguen el §6 de `docs/niveles/nules-nivel.md` y `estaciones/nules.json` (Álvaro); las posiciones están en el §8 de ese documento. **Las aprobó Lucía el 8-nov a las 13:45**, con un solo retoque, ya hecho: el cristal de la farola apagada pasa a 6c7874. Medido sobre el LOD1 a tamaño de juego, ninguna pieza neutra pasa del 0,5 % de píxeles en la banda de la horda. La puerta doble se queda igual que la sencilla aprobada, por el HESCO. Las hojas están en /mnt/project-files/docs/arte/modelado-3d/nules/.

Todas las carpetas llevan la misma `T_Paleta32.png`: material URP/Lit con `_BaseMap` = paleta, como el kit. Las variantes marcadas con -1 en el JSON las elige la semilla. Fuera es −Y de Blender, que en Unity es +Z con yaw 0. El origen está en el suelo. Todas las medidas están SIN VERIFICAR.

| Carpeta | Pieza | Origen y orientación | Hijos animables |
|---|---|---|---|
| edificio_viajeros | Edificio de viajeros de 21 × 17 m. Es un montaje del kit aprobado: 5 módulos Nules de dos plantas, piñón escalonado con óculo en los dos testeros y cuerpo trasero de una planta. v0–v4 = estados de la D-N124: Abandonada, Segura, EnLiberacion, Caida y Liberada | Centro de la huella. La fachada está en y −8,5: en (115; 104,5) va con yaw 180 | — |
| marquesina_adosada | Marquesina de celosía de 21 × 16,8 m (fotos N1 de Paula). Variantes: foto, chapas arrancadas, tramo hundido, solo estructura, y fortificada | Centro de la planta. La fachada está en y +8,4 y el borde del andén en −8,4: en (115; 87,6) va con yaw 180 | — |
| puerta_via_doble | Puerta de vía doble de 7,4 m para dos vías a 3,8 m: la puerta de vía ensanchada, con 5 variantes × 4 estados (`_v<a>_<Variante>_e<n>_<Estado>`). La v4 lleva dos anclajes de catenaria | Centro del hueco, entre las dos vías (con yaw 0, vía 2 en x −1,9 y vía 1 en +1,9). Fuera = −Y | `Hoja_O` y `Hoja_E` (bisagras en ∓3,69): abrir = girar +90° y −90° hacia dentro |
| muelle_carga | Módulo de muelle de 10 × 5,8 m y 1,2 m de alto. Variantes: 0 liso, 1 con carga, 2 roto, 3 rampa de 12 m y 4 cabeza oeste con topes | Centro de la planta. El canto mira a −Y (la vía del apartado), a 1,7 m de su eje | — |
| nave | Nave del polígono en módulos de 9,98 m (`Modulo`) con dos testeros de 0,3 m (`Testero`). Estados: Intacta, PersianaAbierta, CubiertaHundida, Quemada y Saqueada. Muros de 7 m y cumbrera a 8,5 m | Módulo: centro de su planta de 10 × 28 m, con la persiana en −Y (la calle). Testero: centro del muro, con la cara de fuera en −X; el de +X va con giro 180°. Nave de 60,6 m = 6 módulos en x −25 … 25 y testeros en ±30,15 | — |
| urbano | Palmera datilera (v0 de 9 m, v1 curvada de 12 m, v2 joven, v3 seca, v4 tronchada), farola (v0 fernandina, v1 de brazo, v2 de dos faroles, v3 doblada, v4 caída) y banco (v0 de listones, v1 de hormigón, v2 sin respaldo, v3 roto, v4 volcado). Sin colisión | Pie del tronco o de la farola; el banco, en su centro y de cara a −Y | — |
| kit | `SM_KitVia_v2` y `v3`: vía de tercer carril, con un carril más a 1,435 m del de −Y (la v3 tiene un carril levantado). `SM_KitAnden_F11` y `_F7`: módulos de andén de 10 m con 11 y 7 m de fondo, en las variantes 0–4 del kit | Como el kit: centro del tramo, con el borde hacia −Y (el andén de 7 m va con yaw 180) | — |

- La topera es la que ya teníamos, `via/topera` v0–v5 (entrega de Sagunto). Mira a −X: va en (185; 0; 49) con yaw 0.
- Del kit (sin cambios): SM_KitMarquesina, la cooperativa (v4 como almacén citrícola), casas, bloques, catenaria, HESCO, coches civiles y el pino de Bétera.
- El montaje de prueba `estacion_nules` (60 m) queda superado por estas piezas. No se entrega.
- Auditoría y presupuesto OK en las 148 mallas.

Código: TBD `claude/modelado-3d-skill`, de34707 y 4536f8f (retoque de la farola) (`bloques/edificio_viajeros_nules.py`, `marquesina_adosada.py`, `puerta_via_doble.py`, `muelle_carga.py`, `nave_poligono.py`, `urbano.py`, `kit_via.py` y `kit_anden.py`).
