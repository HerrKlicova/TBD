# Shaders de la estética del zum (Hito 1) · Bruno, 11-11

Lo que hace falta para que la escena Mundo (rama `marcos/mundo-zum`) se vea como las láminas de la «Estética del zum» (https://claude.ai/artifact/NeREWjA1C6kJpy9Ug6ysuh; generador en `claude/zum-estetica`, `Art/Codigo/zum/build_zum.py`). Rama: `bruno/zum-shaders`, sacada de `marcos/mundo-zum` (ac8757ea). No toca `MundoBootstrap.cs`: los cambios para enchufarlo están abajo, para que los haga Marcos.

**Compila en la build Mundo6 de Marcos (11-11).** En esa build todo salía muy oscuro: era gamma. Lo que se dibuja desde OnGUI con `Graphics.DrawTexture` llega a la pantalla sin pasar a sRGB, así que cada color salía con su valor lineal (303424 se veía 080904). Corregido en ZumPantalla y ZumIcono con `_Codificar` (lo pone `ZumEstetica.CodificarGamma`, true en proyecto lineal). La paleta de ZumSuelo estaba bien.

**Cómo comprobarlo** con la captura del mapa a 1800 m (`shaders_mapa_1800.png`): el MAPA no lleva luz ni calima, así que los píxeles tienen que ser exactamente de la paleta. Los más frecuentes deben ser 303424 (banda baja), 181c30 (mar), 48403c (banda media), 50541c (trama de naranjal) y 404460 (marjal). En Mundo6 eran 080904, 020308, 110d0c, 141703 y 0d0f1e, que pasados de lineal a sRGB dan justo esos colores. Si después de la corrección salen más claros que la paleta (por ejemplo, 303424 como 5f6350), es sRGB dos veces: poner `ZumEstetica.CodificarGamma = false`.

Antes de esa build: Al abrir el proyecto, mira la consola. Si un shader no compila, `ZumEstetica` avisa una vez con `[ZUM] no carga el shader …` y deja lo de antes (URP/Lit y `GUI.DrawTexture`), así que la escena no se rompe.

## Archivos

| Archivo | Qué es |
|---|---|
| `Assets/TBD/Resources/Zum/ZumSuelo.shader` | `TBD/ZumSuelo`: suelo del mundo. Usa la paleta de 32 por uso del suelo y hace el fundido Bayer entre el MAPA y el suelo de día. Con luz y sombras de día, sin luz en el mapa. |
| `Assets/TBD/Resources/Zum/ZumPantalla.shader` | `TBD/ZumPantalla`: pasa la RT a pantalla con la calima del nivel. |
| `Assets/TBD/Resources/Zum/ZumIcono.shader` | `TBD/ZumIcono`: los iconos, con el relleno teñido, contorno 0f120c y entrada o salida tramada. |
| `Assets/TBD/Resources/Zum/ZumTrazado.shader` | `TBD/ZumTrazado`: cintas del trazado de vía (válido, inválido y tendiéndose), para Iker. |
| `Assets/TBD/Scripts/Mundo/ZumEstetica.cs` | Clase estática con los umbrales, colores y calima, los iconos por tamaño en pantalla, los materiales y el dibujo en OnGUI. |

Los shaders están en `Resources/Zum`, así que entran en la build sin tocar «Always Included Shaders». No es lo que le pasa al Terrain, que salía magenta. Las mallas de 128 m siguen como están: solo les cambia el material.

## Cómo enchufarlo en MundoBootstrap (cinco cambios)

1. **Suelo.** En `MontarTerrenoAlvaro`, después de leer `up` (los `Color32` del PNG de usos):
   ```csharp
   Material zum = up != null ? ZumEstetica.MaterialSuelo(ZumEstetica.TexturaUsos(up, uw, uh), naranjos3D: true) : null;
   ```
   Al crear cada trozo, usa `mr.sharedMaterial = zum != null ? zum : mat;`.
   Para el mar, después de crear `mar`: `var mm = zum != null ? ZumEstetica.MaterialMar(zum.GetTexture("_Usos") as Texture2D) : null; if (mm != null) mar.GetComponent<MeshRenderer>().sharedMaterial = mm;`.
   No pases el PNG cargado con `LoadImage` directamente: Unity lo lee en sRGB y los ids (0–10) salen mal. `TexturaUsos` lo copia a R8 lineal.
2. **Altura.** En `Update`, después de suavizar `_altura`: `ZumEstetica.Actualizar(_altura);`.
3. **Calima.** En `OnGUI`, cambia `GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _rt, ScaleMode.StretchToFill, false);` por `ZumEstetica.DibujarPantalla(new Rect(0, 0, Screen.width, Screen.height), _rt, _altura);`. Los iconos y textos que se dibujan después no llevan calima, como debe ser.
4. **Iconos.** Cambia los cuadrados blancos por los dibujos:
   ```csharp
   int px = ZumEstetica.IconoPxPantalla(_altura);
   ZumEstetica.DibujarIcono(new Vector2(sp.x, sp.y), "estacion", px, ZumEstetica.Hueso);
   ZumEstetica.DibujarIcono(new Vector2(c.x, c.y), "soldado", px, ZumEstetica.Hueso);
   ```
   El tren lleva el color de su línea (`ZumEstetica.Lineas[0]`) y gira con su rumbo (`giro` en cuartos de vuelta). La regla dice que lo que manda es el tamaño del modelo en pantalla, no el nivel: `ZumEstetica.EsIcono(ZumEstetica.ModeloPx720(_cam, pie, 1.8f))`. Con la cámara de Marcos (FOV 32°), un soldado de 1,8 m baja de 10 px a unos 225 m de distancia, más o menos donde está ahora `AlturaIconos = 250`, así que la constante puede quedarse. Para que el icono entre tramándose, guarda un `float vis` por unidad, haz `vis = ZumEstetica.Tramar(vis, esIcono, dt)` y pásalo como `visible`.
5. **Giro.** La regla dice que la cámara vuelve al norte al subir a COMARCA, por encima de 250 m. Ahora lo hace por encima de 900 m. Para seguir la regla: `if (!ZumEstetica.PuedeGirar(_altura)) _yaw = Mathf.MoveTowardsAngle(_yaw, 0f, 225f * dt);` (0,4 s para 90°), y que Q/E solo giren si `PuedeGirar`.

## Propiedades

### TBD/ZumSuelo
| Propiedad | Por defecto | Qué hace |
|---|---|---|
| `_Usos` | negro | Ids de usos del suelo en R8 lineal, sin filtro: 1 texel = 1 m, texel (x, z) = metros (X, Z). La hace `ZumEstetica.TexturaUsos`. |
| `_UsoFijo` | −1 | −1 lee `_Usos`. 0 = mar (para el plano del mar). |
| `_Lado` | 2048 | Lado del mundo en m. Fuera de él, olive_black con borde concrete en el mapa. |
| `_UmbralMapa` | 900 | Altura de cámara del cambio MAPA↔COMARCA. |
| `_Fundido` | 0,25 | El fundido va de 900·0,75 a 900·1,25 m, con tramado Bayer 4×4 en píxeles de la RT (alfa binaria). |
| `_Relieve` | 0,5 | Cuánto pesa la luz en el suelo de día. 0 da la paleta pura de las láminas y 1 el Lambert entero con sombras. Con 0,5 se lee la paleta y se ven el relieve y las sombras de casas y naranjos. |
| `_NaranjosPintados` | 0 | 1 pinta copas de naranjo en el suelo, como las láminas. Con 0 solo se pinta el surco de la hilera, porque Marcos ya tiene naranjos 3D. |
| `_ZumAltura` (global) | — | Altura de la cámara. La pone `ZumEstetica.Actualizar`. |

Qué dibuja:
- **Día.** Cada uso tiene su dibujo: huerta en parcelas de 34 × 46 m (labradas con surcos, verdes o en barbecho, con lindes), naranjal, secano, monte, marjal con charcos, cauce de grava con borde claro, carretera (la franja de usos mide unos 11 m: 7 m de asfalto 6c7874 con motas 94948c y 2 m de arcén 48403c a cada lado, para que case con la losa de 7 m del paso a nivel de Teo), explanada, caserío, playa y mar.
  - De cerca (menos de 0,5 m por píxel), el borde entre usos se rompe con ruido de ±1,2 m.
  - En SOLDADO (menos de 40 m) salen las grietas.
- **Mapa.** Bandas de altura (< 15 m, < 45 m, el resto), la ladera al noroeste un escalón más clara, curvas cada 5 m y maestras cada 25, una trama sobre naranjales y marjal, el cauce, la carretera, las explanadas, el mar navy y la costa en 94948c.

### TBD/ZumPantalla
| Propiedad | Qué hace |
|---|---|
| `_MainTex` | La RT del mundo. |
| `_CalimaColor` | a27e5a (D-N197). |
| `_Calima` | 0–1. La pone `ZumEstetica.CalimaDe(altura)`: 0 % en SOLDADO, 10 % en PUEBLO y 22 % en COMARCA, con rampas en el ±25 % de cada umbral. Va a 0 a medida que los píxeles pasan a mapa. |

### TBD/ZumIcono
| Propiedad | Qué hace |
|---|---|
| `_MainTex` | La textura que da `ZumEstetica.Icono(nombre, px, giro)`: R = 1 relleno, R = 0 contorno, A = máscara. Dibujos: `tren`, `tren_parado`, `soldado`, `estacion`, `cuadrilla` y `deposito`. |
| `_Relleno` | Hueso d4d0cc. El tren va del color de su línea. |
| `_Contorno` | 0f120c. |
| `_Visible` | 0–1, tramado Bayer en píxeles de pantalla. |

Tamaño: 32 px en MAPA y COMARCA, 16 en PUEBLO y ninguno en SOLDADO, medidos en la pantalla de 720 de las láminas. `IconoPxPantalla` lo escala a la pantalla real en múltiplos de 16, así que en 1080p salen 48 y 16.

### TBD/ZumTrazado (para Iker)
La malla es una cinta sobre el terreno con uv.x = metros a lo largo del eje.

| Propiedad | Qué hace |
|---|---|
| `_Color` | El color de la cinta. |
| `_Tramado` | 0–1, con 1 = lleno. |
| `_Guion` | Largo de guion en m. 0 = continuo. |

Recetas con `ZumEstetica.MaterialTrazado`:
- **Válido:** banda `(Hueso, 0.55, 0)` del ancho del balasto y el eje `(Hueso, 1, 6)`.
- **Inválido:** banda `(Ambar, 1, 0)` de 6 m sobre otra 2 px más ancha `(Contorno, 1, 0)`. Las causas van en la placa: «Atraviesa el caserío», «Curva demasiado cerrada» y «Cruce demasiado sesgado» (con carretera, por debajo de 30°).
- **Tendiéndose:** `(Hueso, 0.3, 0)`.

## Lo que no está (todavía)
- **La horda como mancha roja tramada desde COMARCA** (982420). Ahora Marcos dibuja puntos de 3 px. Lo siguiente sería un shader de densidad o un tramado en OnGUI.
- **Las placas** (almacén en cajas, «4 cajas por viaje · un viaje cada 7,2 min»). Son interfaz y van con la interfaz v3.
- **Casas y unidades en el MAPA.** El plano táctico es un dibujo, así que las casas 3D deberían ocultarse por encima de 900·1,25 m o llevar un material plano olive_grey. SIN VERIFICAR cómo queda en la escena.
- **La luz del suelo de día con `_Relieve` 0,5** es un punto de partida, SIN VERIFICAR con la luz de Marcos (sol 1,2 y ambiente 0,45). Si queda oscuro o quemado, se ajusta ese número sin tocar la paleta.
