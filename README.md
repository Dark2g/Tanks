Network/
    NetworkGameManager.cs       # Singleton persistente: UGS Auth + Relay + Lobby + NGO
    NetworkTankMovement.cs      # Movimiento con ServerRpc (autoridad en servidor)
    NetworkTankShooting.cs      # Disparo con ServerRpc, shell spawneado en servidor
    NetworkTankHealth.cs        # Vida como NetworkVariable, daño server-authoritative
    NetworkShellExplosion.cs    # Explosión: física y daño en servidor, VFX via ClientRpc
    OnlineGameManager.cs        # Game loop online con NetworkVariables y RPC
    OnlineColorApplier.cs       # Aplica color del tanque en todos los clientes

UI/
    MainMenuController.cs       # Lógica del menú: local / host / join / quit

Editor/
    MainMenuSceneBuilder.cs     # Herramienta de editor para generar la escena MainMenu

- Arquitectura de red
Relay sirve como intermediario: ningún dispositivo necesita abrir puertos ni IP pública
Lobby almacena el join code dentro de sus datos públicos para que el cliente lo recupere
El host es servidor autoritative; toda la física, el daño y el estado del juego viven en él
Los clientes solo envían input via ServerRpc y reciben estado via ClientRpc y NetworkVariable

Pasos manuales necesarios en el editor
1. Generar la escena del menú
Desde el menú del editor: Tools > Tanks > Build Main Menu Scene. Esto crea Assets/Scenes/MainMenu.unity y la añade al Build Settings automáticamente.

2. Configurar el Build Settings
Ve a File > Build Settings y asegúrate de que el orden es:

Assets/Scenes/MainMenu.unity (índice 0)
Assets/Scenes/_Complete-Game.unity (índice 1)
3. Vincular Unity Gaming Services al proyecto
Ve a Edit > Project Settings > Services y vincula tu proyecto de Unity Dashboard. Relay y Lobby requieren un Project ID activo. Crea el proyecto en dashboard.unity3d.com si aún no tienes uno.

4. Crear el prefab de tanque de red
Duplica tu prefab de tanque existente (CompleteTank) y llámalo NetworkTank. Sobre él:

Añade NetworkObject (componente base de NGO; sin él no puede spawnearse en red)
Sustituye TankMovement → NetworkTankMovement
Sustituye TankShooting → NetworkTankShooting
Sustituye TankHealth → NetworkTankHealth
Añade OnlineColorApplier
Registra el prefab en el NetworkManager (componente que deberás añadir a un GameObject en la escena del juego) en la lista Network Prefabs
5. Configurar la escena _Complete-Game para el modo online
Añade un GameObject vacío OnlineGameManager con el componente OnlineGameManager. Asígnale:

Referencia al CameraControl existente
Referencia al Text de mensajes
El prefab NetworkTank
Los Transform de los spawn points
6. Añadir el NetworkManager al proyecto
En _Complete-Game, añade un GameObject NetworkManager con los componentes:

NetworkManager (del paquete NGO)
UnityTransport (del paquete NGO)
Activa Enable Scene Management en el NetworkManager