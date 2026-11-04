# 📱 Guía Maestra de Configuración: AR Physics Sandbox & Multi-Tool

¡Bienvenido al **AR Physics Sandbox & Multi-Tool**! Esta guía detalla la instalación, configuración de escena, arquitectura de componentes, conexión de inspectores y despliegue para Android e iOS en **Unity 2022.3 LTS o 2023 / 6000 (Unity 6)** con **AR Foundation 5.x / 6.x**.

---

## 📋 Índice
1. [Requisitos Previos del Sistema](#1-requisitos-previos-del-sistema)
2. [Instalación de Paquetes (Package Manager)](#2-instalación-de-paquetes-package-manager)
3. [Configuración del Proyecto (Project Settings)](#3-configuración-del-proyecto-project-settings)
4. [Generador de Prefabs con 1 Clic (Editor Tool)](#4-generador-de-prefabs-con-1-clic-editor-tool)
5. [Estructura y Jerarquía de la Escena](#5-estructura-y-jerarquía-de-la-escena)
6. [Física Real sobre Planos AR (Colisiones Mesa/Piso)](#6-física-real-sobre-planos-ar-colisiones-mesapiso)
7. [Biblioteca de Imágenes de Referencia (Image Tracking)](#7-biblioteca-de-imágenes-de-referencia-image-tracking)
8. [Matriz de Conexión del Inspector](#8-matriz-de-conexión-del-inspector)
9. [Guía de Controles y Mecánicas Jugables](#9-guía-de-controles-y-mecánicas-jugables)
10. [Build & Troubleshooting Común](#10-build--troubleshooting-común)

---

## 1. Requisitos Previos del Sistema

- **Versión de Unity recomendada:** Unity 2022.3 LTS (o Unity 6 / 2023.2+)
- **Render Pipeline:** Universal Render Pipeline (URP) o Built-in Render Pipeline (los scripts y generador soportan ambos automáticamente).
- **Dispositivo Físico de Pruebas:**
  - **Android:** Dispositivo con soporte oficial para [Google Play Services for AR (ARCore)](https://developers.google.com/ar/devices), Android 7.0 (API Nivel 24) o superior.
  - **iOS:** iPhone 6s o superior / iPad Pro con chip A9 o superior con iOS 12+ (para Face Tracking con cámara TrueDepth se requiere iPhone X o superior / iPad Pro con Face ID).

> [!WARNING]
> La Realidad Aumentada que involucra cámara, acelerómetros y sensores de profundidad **NO se puede probar completamente en el Game View del Editor estándar** sin el simulador XR (*XR Environment Simulator*) o un build a un dispositivo real conectado por USB.

---

## 2. Instalación de Paquetes (Package Manager)

Abre **Window > Package Manager** y verifica o instala los siguientes paquetes:

| Nombre del Paquete | Identificador de Paquete (Add by name) | Versión Recomendada | Propósito |
|---|---|---|---|
| **AR Foundation** | `com.unity.xr.arfoundation` | 5.1.x o 6.0.x | Núcleo de AR multiplataforma |
| **Google ARCore XR Plugin** | `com.unity.xr.arcore` | 5.1.x o 6.0.x | Subsystem para dispositivos Android |
| **Apple ARKit XR Plugin** | `com.unity.xr.arkit` | 5.1.x o 6.0.x | Subsystem para dispositivos iOS (World & Face) |
| **Apple ARKit Face Tracking** | `com.unity.xr.arkit-face-tracking` | 5.1.x o 6.0.x | Requerido en AR Foundation 5+ para Modo Rostro en iOS |
| **Input System** | `com.unity.inputsystem` | 1.7.x+ | Sistema de entrada moderno de Unity |
| **TextMeshPro** | `com.unity.textmeshpro` | 3.0.x+ | Fuentes vectoriales para la UI |

### Procedimiento de Instalación:
1. En **Package Manager**, haz clic en el ícono **`+`** (esquina superior izquierda).
2. Selecciona **"Add package by name..."**.
3. Pega el identificador (ejemplo: `com.unity.xr.arfoundation`) y presiona **Add**.
4. Repite para `com.unity.xr.arcore` y `com.unity.xr.arkit`.

---

## 3. Configuración del Proyecto (Project Settings)

### A. XR Plug-in Management
1. Ve a **Edit > Project Settings > XR Plug-in Management**.
2. En la pestaña **Android (ícono de robot)**:
   - Marca la casilla **Google ARCore**.
3. En la pestaña **iOS (ícono de manzana)**:
   - Marca la casilla **Apple ARKit**.

### B. Player Settings - Android
1. Ve a **Edit > Project Settings > Player > Android (Pestaña)**:
   - **Other Settings > Identification:**
     - **Minimum API Level:** `Android 7.0 'Nougat' (API Level 24)` o superior.
     - **Target API Level:** `Automatic (highest installed)` o API 34.
   - **Other Settings > Configuration:**
     - **Scripting Backend:** `IL2CPP` (obligatorio para 64-bit).
     - **Target Architectures:** Marca `ARM64` (desmarca ARMv7 si solo apuntas a 64-bit modernos).
     - **Active Input Handling:** `Both` (garantiza compatibilidad total con `Input.touchCount` y el nuevo Input System simultáneamente).
     - **Auto Graphics API:** Desmarca y asegúrate de que **Vulkan** esté por debajo de **OpenGLES3** o remueve Vulkan si tu versión de ARCore específica presenta conflictos de renderizado de fondo de cámara.

### C. Player Settings - iOS
1. Ve a **Edit > Project Settings > Player > iOS (Pestaña)**:
   - **Other Settings > Identification:**
     - **Target minimum iOS Version:** `12.0` o superior.
     - **Architecture:** `ARM64`.
   - **Other Settings > Configuration:**
     - **Camera Usage Description:** `"Se requiere acceso a la cámara para la experiencia de Realidad Aumentada y detección de superficies."`
     - **Face Tracking Usage Description (si aplica):** `"Se requiere la cámara frontal para el seguimiento de expresiones faciales y accesorios."`
     - **Active Input Handling:** `Both`.

---

## 4. Generador de Prefabs con 1 Clic (Editor Tool)

Hemos incluido una herramienta de editor automatizada para que no tengas que modelar ni importar ningún asset 3D externo.

1. En la barra superior de Unity, ve al menú:
   **`Tools > AR Sandbox > Generate Demo Prefabs`**
2. La herramienta creará automáticamente la carpeta `Assets/Prefabs/ARSandbox/` con los siguientes 9 prefabs configurados con físicas, materiales, tags y capas:
   - 🟦 **`Block.prefab`**: Cubo azul con `Rigidbody` y `BoxCollider`.
   - 🟩 **`Ramp.prefab`**: Rampa inclinada verde para hacer rodar o deslizar objetos.
   - 🎯 **`Target.prefab`**: Cilindro rojo con diana amarilla para derribar.
   - 🚀 **`Projectile.prefab`**: Esfera naranja con `Rigidbody` y `TrailRenderer` luminoso.
   - ⭕ **`PlacementReticle.prefab`**: Anillo con cruz holográfica blanca semitransparente.
   - 🌀 **`PortalPlatform.prefab`**: Plataforma circular morada con trigger de vórtice/trampolín.
   - 🕶️ **`CyberGlasses.prefab`**: Lentes cibernéticos cian para el rostro.
   - 👑 **`Crown.prefab`**: Corona dorada con púas para la cabeza.
   - 🟨 **`ARDefaultPlane.prefab`**: Plano AR con `MeshCollider` + `BoxCollider` con físicas reales integradas.

---

## 5. Estructura y Jerarquía de la Escena

Crea una nueva escena vacía (`File > New Scene > Basic (URP)` o `Empty`) y organízala exactamente así:

```
📦 ARSandboxScene
│
├── 🎮 XR Origin (Action-based o XR Rig)
│   ├── 📁 Camera Offset
│   │   └── 🎥 Main Camera (con ARCameraManager, ARCameraBackground, TrackedPoseDriver)
│   ├── ⚙️ AR Plane Manager
│   ├── ⚙️ AR Raycast Manager
│   ├── ⚙️ AR Tracked Image Manager
│   └── ⚙️ AR Face Manager
│
├── 🌐 AR Session (GameObject con componente ARSession y ARInputManager)
│
├── 🕹️ Managers (GameObject vacío)
│   ├── [ARModeManager]
│   ├── [ARPlacementAndManipulator]
│   ├── [ARFunPhysicsController]
│   ├── [ARImagePortalTracker]
│   ├── [ARFaceAccessoryController]
│   └── [ARUIManager]
│
├── 📦 SpawnedObjectsContainer (GameObject vacío para agrupar las instancias)
│
└── 🖥️ Canvas (Render Mode: Screen Space - Overlay)
    ├── EventSystem (con InputSystemUIInputModule o StandaloneInputModule)
    ├── 🔲 TopBar
    │   ├── 🔘 BtnModeSandbox
    │   ├── 🔘 BtnModeImage
    │   ├── 🔘 BtnModeFace
    │   ├── 🔤 TxtObjectCount (TMP)
    │   └── 🔤 TxtTrackingStatus (TMP)
    │
    ├── 🔲 CatalogPanel (Anclado abajo)
    │   ├── 🔘 BtnSpawnBlock
    │   ├── 🔘 BtnSpawnRamp
    │   ├── 🔘 BtnSpawnTarget
    │   └── 🔘 BtnClearScene
    │
    ├── 🔲 InspectorPanel (Contextual - flotante en lateral/inferior)
    │   ├── 🎚️ SliderRotation (0 a 360) + 🔤 TxtRotationValue
    │   ├── 🎚️ SliderScale (0.2 a 3.0) + 🔤 TxtScaleValue
    │   ├── 🔘 BtnChangeColor
    │   ├── ☑️ ToggleKinematic ("Fijar / Activar Física")
    │   └── 🔘 BtnDeleteObject
    │
    ├── 🔲 ChaosPanel (Acciones divertidas)
    │   ├── 💥 BtnShoot (Disparar proyectil)
    │   ├── 💣 BtnShockwave (Onda expansiva)
    │   ├── 🛸 BtnZeroG (Gravedad cero)
    │   └── ⏱️ BtnBulletTime (Cámara lenta)
    │
    └── 🔲 FacePanel (Modo Rostro)
        ├── 👑 BtnCycleAccessory (Cambiar accesorio)
        └── ⚡ BtnShootLaser (Disparar láser desde ojos)
```

---

## 6. Física Real sobre Planos AR (Colisiones Mesa/Piso)

### ¿Por qué los objetos caen al vacío en AR por defecto?
El componente `ARPlane` que genera Unity dibuja la malla visual pero **no siempre incluye un collider con grosor**, o usa mallas abiertas donde los objetos con `Rigidbody` que caen rápido atraviesan el plano.

### Solución Implementada:
1. Nuestro prefab `ARDefaultPlane.prefab` (generado por el script de Editor) posee un **`BoxCollider` de 2 cm de espesor (`size.y = 0.02f`)**.
2. En `ARPlacementAndManipulator.cs`, el evento `arPlaneManager.planesChanged` escucha cada vez que un plano se expande en el mundo real y **redimensiona dinámicamente el `BoxCollider`** para cubrir toda la mesa o el suelo detectado:
```csharp
// Fragmento operativo de ARPlacementAndManipulator.cs:
boxCollider.size = new Vector3(plane.size.x, 0.02f, plane.size.y);
boxCollider.center = new Vector3(0f, -0.01f, 0f); // 1cm hacia abajo para alineación rasante
```
3. **Paso indispensable en el Inspector:**
   - Selecciona el GameObject `XR Origin`.
   - En el componente `AR Plane Manager`, arrastra el prefab `Assets/Prefabs/ARSandbox/ARDefaultPlane.prefab` al campo **Plane Prefab**.

---

## 7. Biblioteca de Imágenes de Referencia (Image Tracking)

Para utilizar el **Modo Marcador Mágico**:

1. En la ventana **Project**, haz clic derecho en `Assets/` y selecciona:
   **`Create > XR > Reference Image Library`** (o `Create > XR > Reference Image Library`).
2. Nómbrala `ARReferenceImageLibrary`.
3. Selecciona el archivo creado en el Inspector y haz clic en **Add Image**.
4. Arrastra una textura o foto con buen contraste (logos, portadas de libros, códigos QR artísticos).
5. Marca la casilla **Specify Size** e ingresa el tamaño físico real en metros (ejemplo: `0.15` para 15 cm de ancho).
6. Selecciona `XR Origin` en la jerarquía:
   - En el componente `AR Tracked Image Manager`, asigna `ARReferenceImageLibrary` al campo **Serialized Library**.
   - Asigna `Assets/Prefabs/ARSandbox/PortalPlatform.prefab` al campo **Tracked Image Prefab** (o déjalo nulo; `ARImagePortalTracker` lo creará en tiempo de ejecución si está vacío).

---

## 8. Matriz de Conexión del Inspector

A continuación se muestra la referencia exhaustiva para vincular los GameObjects y componentes en el GameObject `Managers`:

### 1. `ARModeManager`
| Campo | Objeto a arrastrar |
|---|---|
| **Ar Plane Manager** | `XR Origin` (Componente `ARPlaneManager`) |
| **Ar Raycast Manager** | `XR Origin` (Componente `ARRaycastManager`) |
| **Ar Tracked Image Manager** | `XR Origin` (Componente `ARTrackedImageManager`) |
| **Ar Face Manager** | `XR Origin` o `Main Camera` (Componente `ARFaceManager`) |
| **Ar Camera Manager** | `Main Camera` (Componente `ARCameraManager`) |
| **Sandbox Controllers** | GameObject contenedor de controllers de sandbox o dejar vacío (gestión automática) |
| **Image Controllers** | GameObject del portal o dejar vacío |
| **Face Controllers** | GameObject de rostro o dejar vacío |

### 2. `ARPlacementAndManipulator`
| Campo | Objeto a arrastrar |
|---|---|
| **Ar Raycast Manager** | `XR Origin` (`ARRaycastManager`) |
| **Ar Plane Manager** | `XR Origin` (`ARPlaneManager`) |
| **Spawnable Prefabs** | Tamaño 3: `Block.prefab`, `Ramp.prefab`, `Target.prefab` |
| **Reticle Prefab** | `Assets/Prefabs/ARSandbox/PlacementReticle.prefab` |
| **Spawned Objects Container** | `SpawnedObjectsContainer` (GameObject de la escena) |
| **Max Spawned Objects** | `50` |

### 3. `ARFunPhysicsController`
| Campo | Objeto a arrastrar |
|---|---|
| **Placement Manager** | `Managers` (`ARPlacementAndManipulator`) |
| **Projectile Prefab** | `Assets/Prefabs/ARSandbox/Projectile.prefab` |
| **Projectile Force** | `15` |
| **Shockwave Force** | `500` |
| **Zero G Float Amplitude** | `0.5` |

### 4. `ARImagePortalTracker`
| Campo | Objeto a arrastrar |
|---|---|
| **Tracked Image Manager** | `XR Origin` (`ARTrackedImageManager`) |
| **Placement Manager** | `Managers` (`ARPlacementAndManipulator`) |
| **Portal Prefab** | `Assets/Prefabs/ARSandbox/PortalPlatform.prefab` |
| **Vortex Force** | `5` |
| **Trampoline Force** | `10` |

### 5. `ARFaceAccessoryController`
| Campo | Objeto a arrastrar |
|---|---|
| **Face Manager** | `XR Origin` o `Main Camera` (`ARFaceManager`) |
| **Accessory Prefabs** | Tamaño 2: `CyberGlasses.prefab`, `Crown.prefab` |
| **Laser Duration** | `0.5` |
| **Laser Length** | `3` |
| **Laser Color** | Rojo (`#FF0000`) o Cian |

### 6. `ARUIManager`
| Sección | Campo del Inspector | Asignación en la Escena |
|---|---|---|
| **Managers** | `Mode Manager` | `Managers` (`ARModeManager`) |
| | `Placement Manager` | `Managers` (`ARPlacementAndManipulator`) |
| | `Physics Controller` | `Managers` (`ARFunPhysicsController`) |
| | `Image Tracker` | `Managers` (`ARImagePortalTracker`) |
| | `Face Controller` | `Managers` (`ARFaceAccessoryController`) |
| **Barra Superior** | `Btn Mode Sandbox` | Botón Sandbox |
| | `Btn Mode Image` | Botón Marcador |
| | `Btn Mode Face` | Botón Rostro |
| | `Txt Object Count` | TMP de contador de objetos |
| | `Txt Tracking Status`| TMP de estado de tracking |
| **Panel Catálogo**| `Catalog Panel` | Panel raíz inferior |
| | `Btn Spawn Block` | Botón Bloque |
| | `Btn Spawn Ramp` | Botón Rampa |
| | `Btn Spawn Target` | Botón Objetivo |
| | `Btn Clear Scene` | Botón Limpiar |
| **Panel Inspector**| `Inspector Panel` | Panel raíz de inspección contextual |
| | `Slider Rotation` | Slider 0 a 360 |
| | `Slider Scale` | Slider 0.2 a 3.0 |
| | `Btn Change Color` | Botón color aleatorio |
| | `Toggle Kinematic` | Toggle de física/fijar |
| | `Btn Delete Object`| Botón eliminar |
| **Panel Chaos** | `Chaos Panel` | Panel raíz de caos físico |
| | `Btn Shoot` | Botón cañón disparador |
| | `Btn Shockwave` | Botón explosión/onda |
| | `Btn Zero G` | Botón gravedad cero |
| | `Btn Bullet Time` | Botón cámara lenta |
| **Panel Rostro** | `Face Panel` | Panel raíz de filtro facial |
| | `Btn Cycle Accessory`| Botón alternar accesorios |
| | `Btn Shoot Laser` | Botón disparo de ojos láser |

> [!TIP]
> Todos los scripts incorporan búsqueda automática de componentes con `FindObjectOfType<T>()` en sus métodos `Awake()`. Si olvidas conectar alguna referencia entre managers, el sistema la resolverá automáticamente en tiempo de ejecución.

---

## 9. Guía de Controles y Mecánicas Jugables

### 🕹️ Modo Sandbox (Superficies Reales)
1. **Detección:** Apunta la cámara hacia una mesa o piso bien iluminado y muévela suavemente en círculos hasta que aparezca la cuadrícula del plano y la **retícula circular**.
2. **Spawneo:** Elige en el catálogo inferior qué objeto deseas (Bloque, Rampa o Diana) y toca sobre la superficie para posicionarlo.
3. **Selección:** Toca un objeto existente para seleccionarlo. Aparecerá un **anillo cian brillante** en su base y se abrirá el **Panel de Inspección Contextual**.
4. **Manipulación Dinámica:**
   - **Arrastrar:** Toca y desliza el objeto seleccionado para reubicarlo suavemente por el plano real.
   - **Rotación y Escala:** Ajusta los sliders del panel para orientarlo o cambiar su tamaño de 0.2x a 3.0x.
   - **Color:** Presiona el botón de paleta para alternar entre 9 tonalidades vibrantes.
   - **Fijar / Soltar Física:** El toggle `isKinematic` permite congelar el objeto en el aire o dejarlo caer por gravedad sobre la mesa real.

### 💥 Modo Caos (Física Divertida)
- **🚀 Cañón AR (Shoot):** Dispara esferas luminosas con estela de partículas desde la cámara hacia el centro de la pantalla para derribar torres de bloques.
- **💣 Onda Expansiva (Shockwave):** Calcula el centro de masa de todos tus objetos y lanza una fuerza radial de 500 N haciendo volar las piezas por el entorno real.
- **🛸 Gravedad Cero (Zero-G):** Apaga la gravedad global e inyecta fuerzas sinusoidales continuas con torque aleatorio; las piezas comenzarán a flotar y girar en el aire como en el espacio exterior.
- **⏱️ Cámara Lenta (Bullet Time):** Reduce el flujo temporal al 25% (`Time.timeScale = 0.25f`) ajustando el paso físico para contemplar colisiones dramáticas en ultra slow-motion.

### 🌀 Modo Marcador Mágico (Image Tracking)
1. Cambia a **Modo Imagen** en la barra superior.
2. Apunta hacia la imagen de referencia impresa o en la pantalla de una tablet.
3. Aparecerá la **Plataforma Interactiva**:
   - **Vórtice Cósmico:** Atrae gravitacionalmente los bloques cercanos hacia su vórtice y los desintegra reduciendo su escala.
   - **Trampolín:** Cualquier bloque o proyectil que toque la plataforma saldrá disparado verticalmente con un impulso elástico.

### 🕶️ Modo Rostro Reactivo (Face Filter)
1. Cambia a **Modo Rostro** en la barra superior. La cámara cambiará automáticamente a la cámara frontal.
2. Mira hacia la cámara: se anclará automáticamente un par de **Lentes Cibernéticos**, una **Corona Dorada** o un **Casco**.
3. Presiona **"Disparar Láser"**: Dos haces láser brillantes saldrán disparados desde las coordenadas biométricas de los ojos hacia adelante.

---

## 10. Build & Troubleshooting Común

### A. Pantalla en Negro en el Dispositivo
- **Causa:** Falta el componente `ARCameraBackground` en la `Main Camera` o falta el permiso de cámara en `AndroidManifest.xml` / `Info.plist`.
- **Solución:** Asegúrate de que la cámara tenga los componentes `AR Camera Manager` y `AR Camera Background`.

### B. Los Objetos Atraviesan la Mesa
- **Causa:** El `AR Plane Manager` no tiene asignado un prefab con collider o el layer del plano no colisiona con el del objeto en la **Physics Collision Matrix**.
- **Solución:** Verifica que el `ARDefaultPlane.prefab` tenga su `BoxCollider` habilitado y asignado en el `Plane Prefab` del `AR Plane Manager`.

### C. La Cámara Frontal no se Activa en Modo Rostro
- **Causa:** El dispositivo no soporta conmutación en caliente de cámara o `ARFaceManager` requiere reinicio de subsistema en versiones antiguas de AR Foundation.
- **Solución:** `ARModeManager` gestiona esto mediante `requestedFacingDirection = CameraFacingDirection.User`. En Android, verifica que ARCore Face Augmented Faces esté soportado por el hardware.

### D. Los Botones de UI no Reaccionan
- **Causa:** Falta un `EventSystem` en la escena o el `StandaloneInputModule` no es compatible con el nuevo Input System.
- **Solución:** Si usas el nuevo Input System, selecciona el `EventSystem` y haz clic en el botón de advertencia **"Replace with InputSystemUIInputModule"**.

---

¡Disfruta construyendo y experimentando con tu nuevo **AR Physics Sandbox & Multi-Tool**! 🚀🎮
