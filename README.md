- Arquitectura de red

Relay sirve como intermediario: ningún dispositivo necesita abrir puertos ni IP pública
Lobby almacena el join code dentro de sus datos públicos para que el cliente lo recupere
El host es servidor autoritativo; toda la física, el daño y el estado del juego viven en él
Los clientes solo envían input via ServerRpc y reciben estado via ClientRpc y NetworkVariable

- Funciones scripts

CameraControl — Mueve y escala la cámara ortográfica para mantener a todos los tanques asignados dentro del encuadre con un margen configurable.

MainMenuController — Controla los paneles del menú principal y las transiciones entre juego local, matchmaking online y salida de la aplicación.
UIDirectionControl — Mantiene la orientación constante de elementos UI en espacio mundo (como barras de vida) independientemente de la rotación del padre.

GameManager — Orquesta el bucle de juego offline: hace spawn de tanques, gestiona las rondas y muestra mensajes de victoria/empate.
TankManager — Clase de datos ([Serializable], sin base class) que el GameManager usa para agrupar color, punto de spawn, estado de control y victorias de cada tanque.
HeartSpawner — Genera corazones de vida de forma procedural en modo offline mediante una cuadrícula con jitter aleatorio y evitación de colisiones.
LandmineSpawner — Misma lógica de spawn procedural que HeartSpawner pero para minas en modo offline.

NetworkGameManager — Inicializa Unity Gaming Services y gestiona el ciclo de vida de sesiones online (Lobby + Relay).
OnlineGameManager — Espejo en red de GameManager (hereda NetworkBehaviour): servidor autoritativo que coordina rondas, victorias y estado de UI replicados con NetworkVariable.
OnlineColorApplier — Sincroniza y aplica el color de material de cada tanque a todos los clientes mediante un ClientRpc.
NetworkTankMovement — El dueño envía su input al servidor vía ServerRpc; el servidor aplica la física de forma autoritativa y NetworkTransform replica la posición al resto.
NetworkTankShooting — El dueño carga el disparo localmente y solicita al servidor que instancie el proyectil vía ServerRpc.
NetworkTankHealth — Gestiona la salud del tanque en red con un NetworkVariable servidor-autoritativo y replica efectos de daño y muerte a todos los clientes.
NetworkShellExplosion — Calcula y aplica daño radial y fuerzas de física a los tanques al impactar, de forma autoritativa en el servidor.
NetworkHeartPickup — Pickup de vida en red: al colisionar con un tanque, lo cura y se despawnea (solo servidor).
NetworkHeartSpawner — Versión online de HeartSpawner: spawn procedural de corazones en red, solo ejecuta lógica en el servidor.
NetworkLandmineExplosion — Mina en red: detecta colisión con tanques, aplica daño y dispara el efecto de explosión en todos los clientes vía ClientRpc.
NetworkLandmineSpawner — Versión online de LandmineSpawner: spawn procedural de minas en red, solo en servidor.
NetworkMinirobotAlly — NPC en red servidor-autoritativo: patrulla waypoints y se detiene para reparar tanques dañados dentro de su radio.

ShellExplosion — Explosión offline: aplica daño radial y fuerzas de física a los tanques según su distancia al impacto.
LandmineExplosion — Explosión offline de mina: aplica daño fijo a los tanques que la pisan y reproduce efectos visuales/sonoros.

TankMovement — Controla el movimiento y rotación local del tanque por input de jugador, gestionando audio de motor y partículas.
TankShooting — Gestiona el disparo local: carga la fuerza de lanzamiento según la duración del input e instancia el prefab del proyectil.
TankHealth — Gestiona la salud del tanque en modo offline: daño, curación y efectos de muerte.
HeartPickup — Pickup de vida offline: restaura salud al tanque al contacto y notifica al spawner para que gestione el reemplazo.
MinirobotAlly — NPC offline: patrulla una ruta de waypoints y cura tanques dañados que entren en su rango de detección y contacto.

