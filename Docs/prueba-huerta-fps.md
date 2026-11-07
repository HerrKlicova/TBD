# Prueba de FPS de la huerta (Teo, 7-nov)

Plan del día: «comprobar los FPS de la huerta con Marcos». La D-N del suelo completo dice que, con 32 FPS de margen, **cualquier pieza nueva que añada instancias se mide antes de entrar**. El suelo de Sara para la build es una **mediana ≥ 290 FPS**. Esta prueba mide cuántos naranjos aguanta la huerta, con la horda dentro.

## Qué es

Son dos archivos nuevos en `Assets/TBD/Scripts/View/`. No cambian nada de lo que ya hay:

- `PrototypeBootstrap.Huerta.cs`: parte `partial` con los métodos internos de la prueba (plantar, horda, cámara, recuentos).
- `HuertaFpsProbe.cs`: solo se activa con `-huertafps`. Sin esa bandera no hace nada.

Con `-huertafps`, el juego pasa del menú al mapa y del mapa a la zona de Sagunto sin la noche, igual que `-stress10k-sagunto`. Después:

1. Planta naranjos (`PlaceVariant("naranjo")`, el mismo LOD1 y las mismas sombras que los de ahora) en la zona 5 del mapa de zonas completo. Van en hileras a lo largo de X, a 5,5 m entre árboles y 5 m entre hileras, al tresbolillo. No pone árboles donde la máscara sin vegetación vale 255 ni duplica las hileras que ya hay.
2. Suelta la horda en la huerta del este (X 186-252, Z 72-124), con la misma alta de zombis que `-stress10k`.
3. Pone la cámara en (218, 97), espera 5 s y mide 40 s.
4. Escribe `Captures/huerta_fps_<etiqueta>.txt` y cierra.

## Banderas

| Bandera | Qué hace | Por defecto |
|---|---|---|
| `-huertafps` | Activa la prueba | (obligatoria) |
| `-huertaradio R` | Planta naranjos a menos de R m de la zona simulada | 0 (solo dentro de la zona) |
| `-huertasin` | No planta nada (medida base) | no |
| `-huertahorda N` | Zombis que se sueltan en la huerta | 10000 |
| `-ortho O` | Zoom de la cámara | 26 (el del juego); 70 es el máximo |
| `-huertatag T` | Etiqueta del archivo de resultados | `r{R}_h{N}_o{O}` |
| `-huertashot` | Guarda una captura a los 30 s | no |

## Recuento de instancias (calculado con docs/niveles/sagunto_zonas_1m.png y la máscara sin vegetación)

| Radio | Naranjos nuevos | Dónde |
|---|---|---|
| 0 | 342 | Solo la huerta de la zona simulada (256 × 128 m) |
| 40 | ~1.150 | Más el borde que se ve con zoom medio |
| 75 | ~2.100 | Un trozo del suelo (75 m) alrededor |
| 150 | ~4.900 | Lo que se ve con ortho 70 |
| 9999 | ~10.100 | Toda la huerta del mapa (278.787 m²) |

Ahora mismo hay unos 50 naranjos (dos hileras al sur y dos al norte). El LOD1 tiene un presupuesto de 100 a 1.100 triángulos y 6 variantes (6 materiales, uno por carpeta). El archivo de resultados da los triángulos y los MeshRenderer reales.

## Procedimiento (después de qa22)

Sobre la build de qa22, en ventana a 1920×1080, como la medida del suelo (352,2 FPS):

```
TBD.exe -huertafps -huertasin                      -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
TBD.exe -huertafps                                 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
TBD.exe -huertafps -huertaradio 75                 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
TBD.exe -huertafps -huertaradio 150 -ortho 70      -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -huertashot
TBD.exe -huertafps -huertaradio 9999 -ortho 70     -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -huertashot
```

Cada pasada dura alrededor de un minuto y deja su propio `.txt` en `Captures/`, con la etiqueta en el nombre: no se pisan. Pasa los cinco `.txt` y las dos capturas a Teo (o a la galería).

Cómo se lee: si el radio 0 pasa, la huerta jugable puede ir llena. Hasta el radio que todavía dé ≥ 290 FPS, el faldón puede llevar árboles sueltos. Si el radio 150 con ortho 70 no pasa, el faldón lejano tiene que ir combinado (una malla por trozo, como la capa C) o con GPU instancing.

## SIN VERIFICAR

- **No está compilado**: en la nube no hay Unity. La sintaxis está comprobada con un analizador de C# y las llamadas usan las mismas APIs que `-stress10k`. Si no compila, es fácil de arreglar; dime el error.
- Doy por hecho que existe el recurso `Resources/Suelo/sagunto_zonas_1m_completo` (756 × 680), el que usa `BuildGroundStreamer`. Si falta, el `.txt` lo dice y la pasada se queda sin árboles.
- Los recuentos de la tabla salen del PNG de Álvaro. En la build pueden variar un poco.
