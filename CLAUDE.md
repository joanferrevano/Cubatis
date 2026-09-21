# Cubatis

Proyecto Unity **6000.6.0f1**, 2D, orientado a móvil vertical (Canvas Scaler
1080x1920). Namespace de todos los scripts: `Cubatis`.

## 1. Descripción general

"Cubatis" es un **juego de mesa tipo "oca" para fiestas/botellón**, pensado
para jugarse en un solo móvil pasándoselo entre jugadores ("party game" local,
no hay red ni multijugador online).

Mecánica principal:
- 2+ jugadores se turnan tirando un dado y avanzando su ficha por un tablero
  en espiral de 60 casillas (START + 58 numeradas + END).
- Al caer en una casilla numerada se abre una **carta de reto** con una frase
  de una categoría (Beber, Yo Nunca, Verdad, Reto, Evento, Hot) que hay que
  cumplir antes de seguir jugando.
- Si la tirada se pasa de la casilla final, la ficha **rebota**: avanza hasta
  el final y retrocede el sobrante (regla clásica del juego de la oca). Solo
  se gana cayendo EXACTO en END.
- La partida termina en cuanto el primer jugador llega a END; el resto de
  jugadores no sigue jugando y se clasifican por su casilla actual en ese
  momento (más avanzado = mejor puesto).

## 2. Escenas y flujo de navegación

Escenas en `Assets/Scenes/`, en este orden en Build Settings:
`SeleccionJugadores → Tablero → Ranking → ModosJuegos`.

| Escena | Función |
|---|---|
| **SeleccionJugadores** | Alta/baja de jugadores (nombre + avatar) antes de jugar. UI generada como GameObjects reales (no prefabs de escena) por la herramienta de editor `ConstructorSeleccionJugadores`. |
| **ModosJuegos** | Selector de modo de juego: 4 tarjetas (`TarjetaModo1-4`) en un Scroll View vertical. |
| **Tablero** | La partida en sí: tablero generado, fichas, dado, cartas de reto, gestor de turnos. |
| **Ranking** | Pantalla final: avatares de los 3 primeros puestos (`JugadorTop1/2/3`) y botón de volver. |

**Flujo real de navegación** (verificado en las escenas, no solo en los
scripts — hay nombres de campo que no coinciden con el destino real):

```
SeleccionJugadores --(botón Empezar, "escenaTablero")--> ModosJuegos
ModosJuegos --(tarjeta "Clásico", única implementada)--> Tablero
ModosJuegos --(BotonAtras)--> SeleccionJugadores
Tablero --(victoria real o botón debug)--> Ranking
Tablero --(BotonSalirTablero, con confirmación)--> ModosJuegos
Ranking --(botón volver)--> SeleccionJugadores
```

⚠️ **No existe una escena `MenuPrincipal`** (ni está en Build Settings). Varios
scripts (`RankingUI`, `BotonSalirTablero`) tienen `"MenuPrincipal"` como valor
**por defecto** del campo `escenaMenu` en el código, pero en las escenas reales
ese campo está reasignado a `SeleccionJugadores` (Ranking) o `ModosJuegos`
(Tablero). Si se añade un menú principal de verdad, hay que crear la escena,
añadirla a Build Settings y re-apuntar esos campos.

## 3. Scripts clave (`Assets/Scripts/`)

**Partida (escena Tablero)**
- `GestorPartida`: orquestador central. Crea las fichas de `Jugadores.Lista`,
  controla turnos, procesa el resultado del dado (incluida la regla de
  rebote), abre `CartaReto` al caer en casilla numerada, y resuelve la
  victoria en `Ganar(jugador)` — único punto de fin de partida, usado tanto
  por una tirada real como por `ForzarVictoriaDebug()`. `Ganar()` ya guarda el
  **ranking completo** (ganador + resto ordenado por casilla actual), no solo
  al ganador.
- `MovimientoFicha`: anima una ficha casilla a casilla (`Mover`) o en dos
  tramos para el rebote (`MoverConRebote`). No sabe nada de turnos ni reglas.
- `Dado`: dado tocable (SpriteRenderer o Image), anima el giro y publica
  `alSalirResultado(int)`. No mueve nada por sí mismo.
- `CartaReto`: popup de carta de reto dibujado en mundo (no Canvas). Debe ir
  **sola** en su propio GameObject (su collider ocupa toda la pantalla). Flujo
  portada → reto (con frase al azar) → cierre → callback a `GestorPartida`.
- `GeneradorTablero`: genera el tablero en espiral 8x8 (64 celdas: 2 START +
  58 numeradas + bloque 2x2 END centrado) **desde el editor** (botón del
  Inspector, no en runtime). Reparte las categorías de las 58 casillas
  numeradas al azar evitando consecutivas.
- `Casilla`: datos de una casilla ya colocada (índice, número, tipo).
- `TipoCasilla`: enum de categorías (Start, Beber, YoNunca, Verdad, Reto,
  Evento, Hot, End).
- `AjusteCamaraTablero`: encuadra la cámara ortográfica al ancho del tablero.
- `FondoJuego`: fuerza el sorting order del fondo por detrás de todo.
- `BotonSalirTablero`: botón de abandonar partida con confirmación; se
  desactiva mientras `CartaReto` está abierta.

**Selección de jugadores (escena SeleccionJugadores)**
- `SeleccionJugadores`: lógica de la pantalla (grid de jugadores + popup de
  alta con nombre y avatar).
- `SlotJugador` / `BotonAvatar`: componentes de prefab, solo pintan datos.
- `Jugadores`: **lista estática** de jugadores (nombre + índice de avatar),
  compartida entre escenas.

**Modo de juego (escena ModosJuegos)**
- `BotonModoJuego`: tarjeta de modo; si `implementado`, carga la escena
  destino; si no, solo avisa por consola (tarjeta clicable pero sin efecto).

**Ranking (escena Ranking)**
- `DatosPartida`: **lista estática** con el ranking final (orden de llegada),
  la rellena `GestorPartida.Ganar()` y la lee `RankingUI`.
- `RankingUI`: pinta `JugadorTop1/2/3` según `DatosPartida.Ranking`; oculta
  los puestos sin jugador (p. ej. Top3 en partidas de 2).

**Debug / temporal**
- `BotonDebugGanar`: botón que llama a `GestorPartida.ForzarVictoriaDebug()`.
  Se autodesactiva si `!Debug.isDebugBuild`, pero el GameObject sigue en la
  escena `Tablero` (ver sección 5).

**Editor (`Assets/Scripts/Editor/`, no se compilan en build)**
- `GeneradorTableroEditor`: inspector custom de `GeneradorTablero` (botones
  para cargar sprites, generar/limpiar/verificar el tablero).
- `ConstructorSeleccionJugadores`: menú `Cubatis > Construir escena Selección
  de Jugadores` — regenera esa escena entera + prefabs `SlotJugador` /
  `BotonAvatar` + avatares placeholder desde cero. También tiene
  `Reasignar prefabs...` y `Migrar ContenedorSlots a Scroll View`.

## 4. Patrones establecidos (seguirlos en código nuevo)

- **Persistencia entre escenas sin `DontDestroyOnLoad`**: `Jugadores` y
  `DatosPartida` son clases **estáticas** con datos simples (listas de
  `Jugador`). Al ser estáticas sobreviven a `SceneManager.LoadScene` sin
  necesidad de un GameObject persistente. `DontDestroyOnLoad` solo se
  justificaría si algo necesitara seguir *actualizándose* o *recibiendo
  eventos* entre escenas — aquí solo hay datos que leer, así que se evita.
- **UI**: Canvas `Screen Space - Overlay` + `CanvasScaler` en modo
  *Scale With Screen Size*, resolución de referencia **1080x1920**, `Match`
  **0.5**. Mantenerlo en cualquier Canvas nuevo.
- **Convención de nombres de eventos**: `UnityEvent`s públicos en español con
  prefijo `al...` en el momento en que ocurren: `alTerminarJuego`,
  `alCaerEnCasilla`, `alCambiarTurno`, `alSalirResultado`,
  `alLlegarACasilla`. Seguir el mismo patrón para eventos nuevos.
- **Carga de assets desde disco solo en editor**: varios scripts
  (`GestorPartida`, `RankingUI`, `CartaReto`, `Dado`) tienen un método
  `#if UNITY_EDITOR` con `[ContextMenu]` para autocargar sprites desde
  `Assets/Boards/...` si el array está vacío, pero **en build hay que
  asignarlos a mano en el Inspector** (el fallback no compila fuera del
  editor).
- **Índices de avatar con módulo seguro**: `avatares[((i % length) + length) % length]`
  se repite en varios sitios (`GestorPartida.AvatarDe`, `RankingUI.AvatarDe`,
  `SeleccionJugadores.ObtenerAvatar`) para no petar con índices fuera de
  rango. Reutilizar el mismo patrón en vez de indexar directo.
- **Un único punto de resolución por flujo**: `GestorPartida.Ganar()` es el
  único sitio que termina la partida (tirada real y botón debug pasan por
  ahí); no duplicar esa lógica en otro sitio.
- **Comentarios**: el código está comentado con XML doc (`/// <summary>`) en
  español explicando el *porqué* de decisiones no obvias, no el *qué* hace
  cada línea. Mantener ese estilo, sin exceso de comentarios triviales.
- **Nombres**: scripts, campos y logs en español; namespace y palabras clave
  de C# en inglés (estándar del lenguaje).

## 5. Estado actual del desarrollo

**Terminado y funcionando:**
- Selección de jugadores (alta/baja, avatar, nombre) con scroll.
- Generación de tablero en espiral (editor) y su reparto de categorías.
- Turnos, tirada de dado, movimiento con animación, regla de rebote y
  victoria exacta en END.
- Cartas de reto (las 6 categorías) con flujo portada → reto → cierre.
- Ranking final: recoge y pinta **todos** los jugadores según su posición de
  llegada (no solo el ganador), ocultando puestos sin jugador.
- Navegación completa entre las 4 escenas reales (ver sección 2).

**A medio hacer:**
- **Solo el modo "Clásico" está implementado.** Las otras 3 tarjetas de
  `ModosJuegos` (**Etílico**, **Hot**, **Pareja**) existen visualmente
  (`BotonModoJuego.implementado = false`) pero no tienen tablero, reglas ni
  destino propios — al pulsarlas solo se loguea un aviso en consola. Todas
  apuntan a `escenaDestino: Tablero` en el Inspector pero eso no se usa
  mientras `implementado` sea `false`.
- No hay escena `MenuPrincipal` (ver aviso en la sección 2) — si se diseña un
  menú principal real, falta crearlo y re-cablear `escenaMenu` en `RankingUI`
  y `BotonSalirTablero`.

**Elementos temporales / debug a recordar antes de publicar:**
- **`BotonDebugGanar`** en la escena `Tablero`: fuerza la victoria instantánea
  del jugador en turno. Se autodesactiva fuera de `Debug.isDebugBuild`, pero
  el GameObject sigue en la escena — **hay que borrarlo** (o al menos
  confirmar que el build de tienda no es "Development Build") antes de
  publicar.
- Fallback de **jugadores de prueba** en `GestorPartida` (bajo
  `#if UNITY_EDITOR`, activado por `jugadoresDePruebaSiVacio`): si se entra a
  `Tablero` con menos de 2 jugadores reales, crea 3 jugadores "Test N"
  automáticamente. Solo corre en el editor, no afecta a builds.
- Los métodos `[ContextMenu] Cargar avatares/caras/sprites desde disco` en
  varios scripts son ayudas de editor para rellenar arrays rápido; no tienen
  efecto en build y no hace falta tocarlos.

**Bugs conocidos pendientes:** ninguno confirmado a día de hoy. El hitbox de
las tarjetas de `ModosJuegos` y la recogida/pintado del ranking con varios
jugadores ya se revisaron y están corregidos.
