// =============================================================================
// ARUIManager.cs
// Administrador principal de la interfaz de usuario para el AR Physics Sandbox.
// Conecta todos los paneles, botones, sliders y toggles con los sistemas AR.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

namespace ARSandbox
{
    /// <summary>
    /// Gestiona toda la interfaz de usuario del sandbox AR: barras de modo,
    /// catálogo de objetos, panel de inspección, acciones de caos y panel facial.
    /// Se suscribe a eventos estáticos de los demás managers y sincroniza el estado visual.
    /// </summary>
    public class ARUIManager : MonoBehaviour
    {
        // =====================================================================
        // CAMPOS SERIALIZADOS
        // =====================================================================

        [Header("=== Referencias a Managers ===")]
        [SerializeField] private ARModeManager modeManager;
        [SerializeField] private ARPlacementAndManipulator placementManager;
        [SerializeField] private ARFunPhysicsController physicsController;
        [SerializeField] private ARImagePortalTracker imageTracker;
        [SerializeField] private ARFaceAccessoryController faceController;

        [Header("=== Barra Superior ===")]
        [SerializeField] private Button btnModeSandbox;
        [SerializeField] private Button btnModeImage;
        [SerializeField] private Button btnModeFace;
        [SerializeField] private TextMeshProUGUI txtObjectCount;
        [SerializeField] private TextMeshProUGUI txtTrackingStatus;
        [SerializeField] private Image[] modeButtonBackgrounds;

        [Header("=== Panel de Catálogo (Inferior) ===")]
        [SerializeField] private GameObject catalogPanel;
        [SerializeField] private Button btnSpawnBlock;
        [SerializeField] private Button btnSpawnRamp;
        [SerializeField] private Button btnSpawnTarget;
        [SerializeField] private Button btnClearScene;
        [SerializeField] private Image[] catalogButtonBackgrounds;

        [Header("=== Panel de Inspección (Contextual) ===")]
        [SerializeField] private GameObject inspectorPanel;
        [SerializeField] private Slider sliderRotation;
        [SerializeField] private Slider sliderScale;
        [SerializeField] private TextMeshProUGUI txtRotationValue;
        [SerializeField] private TextMeshProUGUI txtScaleValue;
        [SerializeField] private Button btnChangeColor;
        [SerializeField] private Toggle toggleKinematic;
        [SerializeField] private Button btnDeleteObject;

        [Header("=== Panel de Acciones Divertidas (Chaos) ===")]
        [SerializeField] private GameObject chaosPanel;
        [SerializeField] private Button btnShoot;
        [SerializeField] private Button btnShockwave;
        [SerializeField] private Button btnZeroG;
        [SerializeField] private Button btnBulletTime;
        [SerializeField] private Image imgZeroGHighlight;
        [SerializeField] private Image imgBulletTimeHighlight;
        [SerializeField] private Color normalButtonColor = Color.white;
        [SerializeField] private Color activeButtonColor = Color.yellow;

        [Header("=== Panel de Modo Rostro ===")]
        [SerializeField] private GameObject facePanel;
        [SerializeField] private Button btnCycleAccessory;
        [SerializeField] private Button btnShootLaser;

        [Header("=== Colores de UI ===")]
        [SerializeField] private Color activeModeColor = new Color(0.3f, 0.8f, 1f);
        [SerializeField] private Color inactiveModeColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color selectedCatalogColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private Color unselectedCatalogColor = Color.white;

        // =====================================================================
        // CAMPOS PRIVADOS
        // =====================================================================

        /// <summary>Índice del prefab actualmente seleccionado en el catálogo (0=Block, 1=Ramp, 2=Target).</summary>
        private int selectedCatalogIndex = 0;

        /// <summary>Referencia cacheada al objeto seleccionado actualmente, para evitar accesos innecesarios.</summary>
        private ManipulableObject currentSelectedObject;

        // =====================================================================
        // CICLO DE VIDA DE UNITY
        // =====================================================================

        /// <summary>
        /// Busca referencias a los managers mediante FindObjectOfType si no fueron
        /// asignadas en el Inspector. Esto sirve como fallback de seguridad.
        /// </summary>
        private void Awake()
        {
            // Fallback: buscar managers en la escena si no están asignados
            if (modeManager == null)
            {
                modeManager = FindObjectOfType<ARModeManager>();
                if (modeManager == null)
                {
                    Debug.LogWarning("[ARUIManager] No se encontró ARModeManager en la escena.");
                }
            }

            if (placementManager == null)
            {
                placementManager = FindObjectOfType<ARPlacementAndManipulator>();
                if (placementManager == null)
                {
                    Debug.LogWarning("[ARUIManager] No se encontró ARPlacementAndManipulator en la escena.");
                }
            }

            if (physicsController == null)
            {
                physicsController = FindObjectOfType<ARFunPhysicsController>();
                if (physicsController == null)
                {
                    Debug.LogWarning("[ARUIManager] No se encontró ARFunPhysicsController en la escena.");
                }
            }

            if (imageTracker == null)
            {
                imageTracker = FindObjectOfType<ARImagePortalTracker>();
                if (imageTracker == null)
                {
                    Debug.LogWarning("[ARUIManager] No se encontró ARImagePortalTracker en la escena.");
                }
            }

            if (faceController == null)
            {
                faceController = FindObjectOfType<ARFaceAccessoryController>();
                if (faceController == null)
                {
                    Debug.LogWarning("[ARUIManager] No se encontró ARFaceAccessoryController en la escena.");
                }
            }
        }

        /// <summary>
        /// Se suscribe a todos los eventos estáticos y agrega listeners a todos los
        /// botones, sliders y toggles de la interfaz.
        /// </summary>
        private void OnEnable()
        {
            // -----------------------------------------------------------------
            // Suscripción a eventos estáticos de los managers
            // -----------------------------------------------------------------
            ARModeManager.OnModeChanged += HandleModeChanged;
            ARPlacementAndManipulator.OnObjectSpawned += HandleObjectSpawned;
            ARPlacementAndManipulator.OnObjectSelected += HandleObjectSelected;
            ARPlacementAndManipulator.OnObjectDeselected += HandleObjectDeselected;
            ARFunPhysicsController.OnZeroGToggled += HandleZeroGToggled;
            ARFunPhysicsController.OnBulletTimeToggled += HandleBulletTimeToggled;
            ManipulableObject.OnDestroyed += HandleObjectDestroyed;

            // -----------------------------------------------------------------
            // Listeners de botones de modo (barra superior)
            // -----------------------------------------------------------------
            if (btnModeSandbox != null)
            {
                btnModeSandbox.onClick.AddListener(OnClickModeSandbox);
            }

            if (btnModeImage != null)
            {
                btnModeImage.onClick.AddListener(OnClickModeImage);
            }

            if (btnModeFace != null)
            {
                btnModeFace.onClick.AddListener(OnClickModeFace);
            }

            // -----------------------------------------------------------------
            // Listeners de botones del catálogo (panel inferior)
            // -----------------------------------------------------------------
            if (btnSpawnBlock != null)
            {
                btnSpawnBlock.onClick.AddListener(OnClickSpawnBlock);
            }

            if (btnSpawnRamp != null)
            {
                btnSpawnRamp.onClick.AddListener(OnClickSpawnRamp);
            }

            if (btnSpawnTarget != null)
            {
                btnSpawnTarget.onClick.AddListener(OnClickSpawnTarget);
            }

            if (btnClearScene != null)
            {
                btnClearScene.onClick.AddListener(OnClickClearScene);
            }

            // -----------------------------------------------------------------
            // Listeners del panel de inspección
            // -----------------------------------------------------------------
            if (sliderRotation != null)
            {
                sliderRotation.minValue = 0f;
                sliderRotation.maxValue = 360f;
                sliderRotation.onValueChanged.AddListener(OnRotationSliderChanged);
            }

            if (sliderScale != null)
            {
                sliderScale.minValue = 0.2f;
                sliderScale.maxValue = 3.0f;
                sliderScale.onValueChanged.AddListener(OnScaleSliderChanged);
            }

            if (btnChangeColor != null)
            {
                btnChangeColor.onClick.AddListener(OnClickChangeColor);
            }

            if (toggleKinematic != null)
            {
                toggleKinematic.onValueChanged.AddListener(OnKinematicToggleChanged);
            }

            if (btnDeleteObject != null)
            {
                btnDeleteObject.onClick.AddListener(OnClickDeleteObject);
            }

            // -----------------------------------------------------------------
            // Listeners del panel de acciones de caos
            // -----------------------------------------------------------------
            if (btnShoot != null)
            {
                btnShoot.onClick.AddListener(OnClickShoot);
            }

            if (btnShockwave != null)
            {
                btnShockwave.onClick.AddListener(OnClickShockwave);
            }

            if (btnZeroG != null)
            {
                btnZeroG.onClick.AddListener(OnClickZeroG);
            }

            if (btnBulletTime != null)
            {
                btnBulletTime.onClick.AddListener(OnClickBulletTime);
            }

            // -----------------------------------------------------------------
            // Listeners del panel de modo rostro
            // -----------------------------------------------------------------
            if (btnCycleAccessory != null)
            {
                btnCycleAccessory.onClick.AddListener(OnClickCycleAccessory);
            }

            if (btnShootLaser != null)
            {
                btnShootLaser.onClick.AddListener(OnClickShootLaser);
            }

            // -----------------------------------------------------------------
            // Inicializar estado visual por defecto
            // -----------------------------------------------------------------
            SetCatalogSelection(selectedCatalogIndex);
            ShowPanel(inspectorPanel, false);
            UpdateObjectCount();

            // Aplicar estado inicial de highlights de ZeroG y BulletTime
            if (physicsController != null)
            {
                SetHighlightImage(imgZeroGHighlight, physicsController.IsZeroGActive);
                SetHighlightImage(imgBulletTimeHighlight, physicsController.IsBulletTimeActive);
            }
            else
            {
                SetHighlightImage(imgZeroGHighlight, false);
                SetHighlightImage(imgBulletTimeHighlight, false);
            }

            // Sincronizar paneles con el modo actual si el modeManager ya tiene un modo establecido
            if (modeManager != null)
            {
                HandleModeChanged(modeManager.CurrentMode);
            }
        }

        /// <summary>
        /// Desuscribe todos los eventos estáticos y elimina todos los listeners de la UI.
        /// </summary>
        private void OnDisable()
        {
            // -----------------------------------------------------------------
            // Desuscripción de eventos estáticos
            // -----------------------------------------------------------------
            ARModeManager.OnModeChanged -= HandleModeChanged;
            ARPlacementAndManipulator.OnObjectSpawned -= HandleObjectSpawned;
            ARPlacementAndManipulator.OnObjectSelected -= HandleObjectSelected;
            ARPlacementAndManipulator.OnObjectDeselected -= HandleObjectDeselected;
            ARFunPhysicsController.OnZeroGToggled -= HandleZeroGToggled;
            ARFunPhysicsController.OnBulletTimeToggled -= HandleBulletTimeToggled;
            ManipulableObject.OnDestroyed -= HandleObjectDestroyed;

            // -----------------------------------------------------------------
            // Remover listeners de botones de modo
            // -----------------------------------------------------------------
            if (btnModeSandbox != null) btnModeSandbox.onClick.RemoveListener(OnClickModeSandbox);
            if (btnModeImage != null) btnModeImage.onClick.RemoveListener(OnClickModeImage);
            if (btnModeFace != null) btnModeFace.onClick.RemoveListener(OnClickModeFace);

            // -----------------------------------------------------------------
            // Remover listeners del catálogo
            // -----------------------------------------------------------------
            if (btnSpawnBlock != null) btnSpawnBlock.onClick.RemoveListener(OnClickSpawnBlock);
            if (btnSpawnRamp != null) btnSpawnRamp.onClick.RemoveListener(OnClickSpawnRamp);
            if (btnSpawnTarget != null) btnSpawnTarget.onClick.RemoveListener(OnClickSpawnTarget);
            if (btnClearScene != null) btnClearScene.onClick.RemoveListener(OnClickClearScene);

            // -----------------------------------------------------------------
            // Remover listeners del panel de inspección
            // -----------------------------------------------------------------
            if (sliderRotation != null) sliderRotation.onValueChanged.RemoveListener(OnRotationSliderChanged);
            if (sliderScale != null) sliderScale.onValueChanged.RemoveListener(OnScaleSliderChanged);
            if (btnChangeColor != null) btnChangeColor.onClick.RemoveListener(OnClickChangeColor);
            if (toggleKinematic != null) toggleKinematic.onValueChanged.RemoveListener(OnKinematicToggleChanged);
            if (btnDeleteObject != null) btnDeleteObject.onClick.RemoveListener(OnClickDeleteObject);

            // -----------------------------------------------------------------
            // Remover listeners del panel de caos
            // -----------------------------------------------------------------
            if (btnShoot != null) btnShoot.onClick.RemoveListener(OnClickShoot);
            if (btnShockwave != null) btnShockwave.onClick.RemoveListener(OnClickShockwave);
            if (btnZeroG != null) btnZeroG.onClick.RemoveListener(OnClickZeroG);
            if (btnBulletTime != null) btnBulletTime.onClick.RemoveListener(OnClickBulletTime);

            // -----------------------------------------------------------------
            // Remover listeners del panel de rostro
            // -----------------------------------------------------------------
            if (btnCycleAccessory != null) btnCycleAccessory.onClick.RemoveListener(OnClickCycleAccessory);
            if (btnShootLaser != null) btnShootLaser.onClick.RemoveListener(OnClickShootLaser);
        }

        /// <summary>
        /// Actualización ligera cada frame: solo actualiza el estado de tracking AR.
        /// </summary>
        private void Update()
        {
            UpdateTrackingStatus();
        }

        // =====================================================================
        // CALLBACKS DE BOTONES (MODO)
        // =====================================================================

        /// <summary>Cambia al modo Sandbox cuando el usuario pulsa el botón correspondiente.</summary>
        private void OnClickModeSandbox()
        {
            if (modeManager != null)
            {
                modeManager.SetModeSandbox();
            }
        }

        /// <summary>Cambia al modo ImageMarker cuando el usuario pulsa el botón correspondiente.</summary>
        private void OnClickModeImage()
        {
            if (modeManager != null)
            {
                modeManager.SetModeImageMarker();
            }
        }

        /// <summary>Cambia al modo FaceFilter cuando el usuario pulsa el botón correspondiente.</summary>
        private void OnClickModeFace()
        {
            if (modeManager != null)
            {
                modeManager.SetModeFaceFilter();
            }
        }

        // =====================================================================
        // CALLBACKS DE BOTONES (CATÁLOGO)
        // =====================================================================

        /// <summary>Selecciona el prefab Bloque (índice 0) y notifica al placement manager.</summary>
        private void OnClickSpawnBlock()
        {
            SetCatalogSelection(0);
            if (placementManager != null)
            {
                placementManager.SetSelectedPrefabIndex(0);
            }
        }

        /// <summary>Selecciona el prefab Rampa (índice 1) y notifica al placement manager.</summary>
        private void OnClickSpawnRamp()
        {
            SetCatalogSelection(1);
            if (placementManager != null)
            {
                placementManager.SetSelectedPrefabIndex(1);
            }
        }

        /// <summary>Selecciona el prefab Objetivo (índice 2) y notifica al placement manager.</summary>
        private void OnClickSpawnTarget()
        {
            SetCatalogSelection(2);
            if (placementManager != null)
            {
                placementManager.SetSelectedPrefabIndex(2);
            }
        }

        /// <summary>Limpia todos los objetos de la escena y oculta el panel de inspección.</summary>
        private void OnClickClearScene()
        {
            if (placementManager != null)
            {
                placementManager.ClearAllObjects();
            }

            // Al limpiar todos los objetos, ocultar el inspector y resetear contador
            ShowPanel(inspectorPanel, false);
            currentSelectedObject = null;
            UpdateObjectCount();
        }

        // =====================================================================
        // CALLBACKS DE BOTONES (INSPECCIÓN)
        // =====================================================================

        /// <summary>Aplica un color aleatorio al objeto seleccionado.</summary>
        private void OnClickChangeColor()
        {
            if (currentSelectedObject != null)
            {
                currentSelectedObject.SetRandomColor();
            }
        }

        /// <summary>Elimina el objeto seleccionado de la escena.</summary>
        private void OnClickDeleteObject()
        {
            if (currentSelectedObject != null)
            {
                // Deseleccionar antes de destruir para evitar referencias inválidas
                currentSelectedObject.SetSelected(false);
                Destroy(currentSelectedObject.gameObject);
                currentSelectedObject = null;
                ShowPanel(inspectorPanel, false);
            }
        }

        /// <summary>Cambia el estado cinemático del objeto seleccionado según el toggle.</summary>
        /// <param name="isKinematic">True para hacer el objeto cinemático (no afectado por físicas).</param>
        private void OnKinematicToggleChanged(bool isKinematic)
        {
            if (currentSelectedObject != null)
            {
                currentSelectedObject.SetKinematic(isKinematic);
            }
        }

        // =====================================================================
        // CALLBACKS DE BOTONES (CAOS / ACCIONES DIVERTIDAS)
        // =====================================================================

        /// <summary>Dispara un proyectil desde la cámara AR.</summary>
        private void OnClickShoot()
        {
            if (physicsController != null)
            {
                physicsController.ShootProjectile();
            }
        }

        /// <summary>Genera una onda de choque que empuja todos los objetos.</summary>
        private void OnClickShockwave()
        {
            if (physicsController != null)
            {
                physicsController.TriggerShockwave();
            }
        }

        /// <summary>Activa o desactiva la gravedad cero en la escena.</summary>
        private void OnClickZeroG()
        {
            if (physicsController != null)
            {
                physicsController.ToggleZeroG();
            }
        }

        /// <summary>Activa o desactiva el efecto de cámara lenta (bullet time).</summary>
        private void OnClickBulletTime()
        {
            if (physicsController != null)
            {
                physicsController.ToggleBulletTime();
            }
        }

        // =====================================================================
        // CALLBACKS DE BOTONES (MODO ROSTRO)
        // =====================================================================

        /// <summary>Cicla al siguiente accesorio facial disponible.</summary>
        private void OnClickCycleAccessory()
        {
            if (faceController != null)
            {
                faceController.CycleAccessory();
            }
        }

        /// <summary>Dispara un láser desde la posición del rostro detectado.</summary>
        private void OnClickShootLaser()
        {
            if (faceController != null)
            {
                faceController.ShootLaser();
            }
        }

        // =====================================================================
        // MANEJADORES DE EVENTOS ESTÁTICOS
        // =====================================================================

        /// <summary>
        /// Maneja el cambio de modo AR. Actualiza los highlights de los botones de modo
        /// y muestra/oculta los paneles correspondientes al modo activo.
        /// </summary>
        /// <param name="mode">Nuevo modo AR activo.</param>
        private void HandleModeChanged(ARMode mode)
        {
            // Actualizar resaltado visual de los botones de modo
            UpdateModeButtonHighlights(mode);

            switch (mode)
            {
                case ARMode.Sandbox:
                    // Modo Sandbox: mostrar catálogo y panel de caos, ocultar el de rostro
                    ShowPanel(catalogPanel, true);
                    ShowPanel(chaosPanel, true);
                    ShowPanel(facePanel, false);
                    // El inspector solo se muestra si hay un objeto seleccionado
                    ShowPanel(inspectorPanel, currentSelectedObject != null);
                    break;

                case ARMode.ImageMarker:
                    // Modo ImageMarker: ocultar todos los paneles principales
                    ShowPanel(catalogPanel, false);
                    ShowPanel(chaosPanel, false);
                    ShowPanel(facePanel, false);
                    ShowPanel(inspectorPanel, false);
                    break;

                case ARMode.FaceFilter:
                    // Modo FaceFilter: solo mostrar el panel facial
                    ShowPanel(catalogPanel, false);
                    ShowPanel(chaosPanel, false);
                    ShowPanel(facePanel, true);
                    ShowPanel(inspectorPanel, false);
                    break;

                default:
                    Debug.LogWarning($"[ARUIManager] Modo AR no reconocido: {mode}");
                    break;
            }

            // Al cambiar de modo, deseleccionar cualquier objeto seleccionado
            if (mode != ARMode.Sandbox && placementManager != null)
            {
                placementManager.DeselectObject();
            }
        }

        /// <summary>
        /// Maneja el evento de un nuevo objeto spawneado. Actualiza el contador de objetos.
        /// </summary>
        /// <param name="obj">Objeto recién spawneado.</param>
        private void HandleObjectSpawned(ManipulableObject obj)
        {
            UpdateObjectCount();
        }

        /// <summary>
        /// Maneja la selección de un objeto manipulable. Muestra el panel de inspección
        /// y sincroniza todos los controles con los valores actuales del objeto.
        /// </summary>
        /// <param name="obj">Objeto que fue seleccionado.</param>
        private void HandleObjectSelected(ManipulableObject obj)
        {
            if (obj == null)
            {
                return;
            }

            currentSelectedObject = obj;

            // Solo mostrar el inspector si estamos en modo Sandbox
            if (modeManager != null && modeManager.CurrentMode == ARMode.Sandbox)
            {
                ShowPanel(inspectorPanel, true);
            }

            // Sincronizar el slider de rotación con la rotación actual del objeto
            if (sliderRotation != null)
            {
                sliderRotation.SetValueWithoutNotify(obj.CurrentRotationY);
                UpdateRotationLabel(obj.CurrentRotationY);
            }

            // Sincronizar el slider de escala con la escala actual del objeto
            if (sliderScale != null)
            {
                sliderScale.SetValueWithoutNotify(obj.CurrentScale);
                UpdateScaleLabel(obj.CurrentScale);
            }

            // Sincronizar el toggle cinemático con el estado actual del Rigidbody
            if (toggleKinematic != null && obj.Rb != null)
            {
                toggleKinematic.SetIsOnWithoutNotify(obj.Rb.isKinematic);
            }
        }

        /// <summary>
        /// Maneja la deselección de objetos. Oculta el panel de inspección y limpia la referencia.
        /// </summary>
        private void HandleObjectDeselected()
        {
            currentSelectedObject = null;
            ShowPanel(inspectorPanel, false);
        }

        /// <summary>
        /// Maneja la destrucción de un objeto manipulable. Actualiza el contador y,
        /// si el objeto destruido era el seleccionado, oculta el inspector.
        /// </summary>
        /// <param name="obj">Objeto que fue destruido.</param>
        private void HandleObjectDestroyed(ManipulableObject obj)
        {
            UpdateObjectCount();

            // Si el objeto destruido era el seleccionado, limpiar la referencia y ocultar el inspector
            if (currentSelectedObject != null && currentSelectedObject == obj)
            {
                currentSelectedObject = null;
                ShowPanel(inspectorPanel, false);
            }
        }

        /// <summary>
        /// Maneja el cambio de estado de gravedad cero. Actualiza el highlight visual del botón.
        /// </summary>
        /// <param name="active">True si la gravedad cero está activa.</param>
        private void HandleZeroGToggled(bool active)
        {
            SetHighlightImage(imgZeroGHighlight, active);
        }

        /// <summary>
        /// Maneja el cambio de estado de bullet time. Actualiza el highlight visual del botón.
        /// </summary>
        /// <param name="active">True si el bullet time está activo.</param>
        private void HandleBulletTimeToggled(bool active)
        {
            SetHighlightImage(imgBulletTimeHighlight, active);
        }

        // =====================================================================
        // MANEJADORES DE SLIDERS
        // =====================================================================

        /// <summary>
        /// Responde al cambio del slider de rotación. Aplica la rotación al objeto
        /// seleccionado y actualiza la etiqueta de texto.
        /// </summary>
        /// <param name="value">Nuevo valor de rotación en grados (0-360).</param>
        private void OnRotationSliderChanged(float value)
        {
            if (currentSelectedObject != null)
            {
                currentSelectedObject.SetRotationY(value);
            }

            UpdateRotationLabel(value);
        }

        /// <summary>
        /// Responde al cambio del slider de escala. Aplica la escala al objeto
        /// seleccionado y actualiza la etiqueta de texto.
        /// </summary>
        /// <param name="value">Nuevo valor de escala (0.2 - 3.0).</param>
        private void OnScaleSliderChanged(float value)
        {
            if (currentSelectedObject != null)
            {
                currentSelectedObject.SetScale(value);
            }

            UpdateScaleLabel(value);
        }

        // =====================================================================
        // MÉTODOS AUXILIARES
        // =====================================================================

        /// <summary>
        /// Actualiza el texto del contador de objetos en la barra superior.
        /// Muestra la cantidad de objetos spawneados actualmente en la escena.
        /// </summary>
        private void UpdateObjectCount()
        {
            if (txtObjectCount == null)
            {
                return;
            }

            int count = 0;
            if (placementManager != null)
            {
                count = placementManager.SpawnedObjectCount;
            }

            txtObjectCount.text = $"Objetos: {count}";
        }

        /// <summary>
        /// Actualiza los highlights de los botones de modo. El botón del modo activo
        /// se resalta con el color activo; los demás se atenúan.
        /// </summary>
        /// <param name="mode">Modo actualmente activo.</param>
        private void UpdateModeButtonHighlights(ARMode mode)
        {
            if (modeButtonBackgrounds == null || modeButtonBackgrounds.Length == 0)
            {
                return;
            }

            // Asignar colores según el modo activo
            // Índice 0 = Sandbox, 1 = ImageMarker, 2 = FaceFilter
            for (int i = 0; i < modeButtonBackgrounds.Length; i++)
            {
                if (modeButtonBackgrounds[i] == null)
                {
                    continue;
                }

                bool isActive = false;
                switch (i)
                {
                    case 0:
                        isActive = (mode == ARMode.Sandbox);
                        break;
                    case 1:
                        isActive = (mode == ARMode.ImageMarker);
                        break;
                    case 2:
                        isActive = (mode == ARMode.FaceFilter);
                        break;
                }

                modeButtonBackgrounds[i].color = isActive ? activeModeColor : inactiveModeColor;
            }
        }

        /// <summary>
        /// Establece la selección visual del catálogo de prefabs. Resalta el botón
        /// seleccionado y atenúa los demás.
        /// </summary>
        /// <param name="index">Índice del botón seleccionado en el catálogo.</param>
        private void SetCatalogSelection(int index)
        {
            selectedCatalogIndex = index;

            if (catalogButtonBackgrounds == null || catalogButtonBackgrounds.Length == 0)
            {
                return;
            }

            for (int i = 0; i < catalogButtonBackgrounds.Length; i++)
            {
                if (catalogButtonBackgrounds[i] == null)
                {
                    continue;
                }

                catalogButtonBackgrounds[i].color = (i == selectedCatalogIndex)
                    ? selectedCatalogColor
                    : unselectedCatalogColor;
            }
        }

        /// <summary>
        /// Muestra u oculta un panel de forma segura, verificando que no sea null.
        /// </summary>
        /// <param name="panel">GameObject del panel a mostrar/ocultar.</param>
        /// <param name="show">True para mostrar, false para ocultar.</param>
        private void ShowPanel(GameObject panel, bool show)
        {
            if (panel != null)
            {
                panel.SetActive(show);
            }
        }

        /// <summary>
        /// Establece el color de una imagen de highlight para indicar si un efecto
        /// está activo o inactivo (ej: ZeroG, BulletTime).
        /// </summary>
        /// <param name="image">Imagen de highlight a actualizar.</param>
        /// <param name="active">True si el efecto está activo.</param>
        private void SetHighlightImage(Image image, bool active)
        {
            if (image != null)
            {
                image.color = active ? activeButtonColor : normalButtonColor;
            }
        }

        /// <summary>
        /// Actualiza la etiqueta de texto que muestra el valor actual de rotación.
        /// </summary>
        /// <param name="value">Valor de rotación en grados.</param>
        private void UpdateRotationLabel(float value)
        {
            if (txtRotationValue != null)
            {
                txtRotationValue.text = $"{value:F0}°";
            }
        }

        /// <summary>
        /// Actualiza la etiqueta de texto que muestra el valor actual de escala.
        /// </summary>
        /// <param name="value">Valor de escala.</param>
        private void UpdateScaleLabel(float value)
        {
            if (txtScaleValue != null)
            {
                txtScaleValue.text = $"{value:F2}x";
            }
        }

        /// <summary>
        /// Actualiza el texto de estado del tracking AR en la barra superior.
        /// Consulta el estado de ARSession para mostrar información relevante al usuario.
        /// </summary>
        private void UpdateTrackingStatus()
        {
            if (txtTrackingStatus == null)
            {
                return;
            }

            // Obtener el estado actual de la sesión AR
            switch (ARSession.state)
            {
                case ARSessionState.None:
                    txtTrackingStatus.text = "Tracking: Inicializando...";
                    break;
                case ARSessionState.Unsupported:
                    txtTrackingStatus.text = "Tracking: No Soportado";
                    break;
                case ARSessionState.CheckingAvailability:
                    txtTrackingStatus.text = "Tracking: Verificando...";
                    break;
                case ARSessionState.NeedsInstall:
                    txtTrackingStatus.text = "Tracking: Requiere Instalación";
                    break;
                case ARSessionState.Installing:
                    txtTrackingStatus.text = "Tracking: Instalando...";
                    break;
                case ARSessionState.Ready:
                    txtTrackingStatus.text = "Tracking: Listo";
                    break;
                case ARSessionState.SessionInitializing:
                    txtTrackingStatus.text = "Tracking: Iniciando Sesión...";
                    break;
                case ARSessionState.SessionTracking:
                    txtTrackingStatus.text = "Tracking: Activo";
                    break;
                default:
                    txtTrackingStatus.text = "Tracking: Desconocido";
                    break;
            }
        }
    }
}
