using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARSandbox
{
    /// <summary>
    /// Modo del portal interactivo generado sobre imágenes rastreadas.
    /// Vortex: atrae y encoge objetos cercanos.
    /// Trampoline: rebota objetos que se acercan hacia arriba.
    /// </summary>
    public enum PortalMode
    {
        Vortex,
        Trampoline
    }

    /// <summary>
    /// Rastreador de imágenes AR que genera plataformas interactivas (Vórtice o Trampolín)
    /// en la ubicación de las imágenes de referencia detectadas. Los portales afectan
    /// a los ManipulableObject generados por ARPlacementAndManipulator.
    /// </summary>
    public class ARImagePortalTracker : MonoBehaviour
    {
        #region Campos Serializados

        [Header("Referencias AR")]
        [SerializeField, Tooltip("Gestor de imágenes rastreadas de ARFoundation.")]
        private ARTrackedImageManager trackedImageManager;

        [SerializeField, Tooltip("Referencia al gestor de colocación para acceder a los objetos generados.")]
        private ARPlacementAndManipulator placementManager;

        [Header("Portal")]
        [SerializeField, Tooltip("Prefab del portal. Si es nulo, se crea un visual programáticamente.")]
        private GameObject portalPrefab;

        [Header("Modo Vórtice")]
        [SerializeField, Tooltip("Fuerza de atracción del vórtice.")]
        private float vortexForce = 5f;

        [SerializeField, Tooltip("Radio de alcance del efecto del vórtice.")]
        private float vortexRange = 2f;

        [Header("Modo Trampolín")]
        [SerializeField, Tooltip("Fuerza de rebote del trampolín.")]
        private float trampolineForce = 10f;

        [Header("Efectos")]
        [SerializeField, Tooltip("Velocidad de encogimiento de objetos dentro del vórtice.")]
        private float shrinkRate = 0.5f;

        #endregion

        #region Propiedades Públicas

        /// <summary>
        /// Modo actual del portal: Vórtice o Trampolín.
        /// </summary>
        public PortalMode CurrentPortalMode { get; private set; } = PortalMode.Vortex;

        #endregion

        #region Campos Privados

        /// <summary>
        /// Diccionario que mapea el ID rastreable de cada imagen a su portal instanciado.
        /// </summary>
        private Dictionary<TrackableId, GameObject> spawnedPortals = new Dictionary<TrackableId, GameObject>();

        /// <summary>
        /// Velocidad de rotación de los portales en grados por segundo.
        /// </summary>
        private const float PortalSpinSpeed = 90f;

        /// <summary>
        /// Distancia mínima para activar el rebote del trampolín.
        /// </summary>
        private const float TrampolineBounceRange = 0.15f;

        /// <summary>
        /// Tiempo de enfriamiento entre rebotes consecutivos del trampolín (en segundos).
        /// </summary>
        private const float TrampBounceCooldown = 0.5f;

        /// <summary>
        /// Escala mínima antes de destruir un objeto en el vórtice.
        /// </summary>
        private const float MinScaleBeforeDestroy = 0.05f;

        /// <summary>
        /// Diccionario de enfriamiento de rebotes por objeto para evitar rebotes repetidos cada frame.
        /// </summary>
        private Dictionary<int, float> bounceCooldowns = new Dictionary<int, float>();

        #endregion

        #region Ciclo de Vida de Unity

        /// <summary>
        /// Inicialización temprana: busca referencias faltantes con FindObjectOfType.
        /// </summary>
        private void Awake()
        {
            // Buscar el gestor de imágenes rastreadas si no fue asignado
            if (trackedImageManager == null)
            {
                trackedImageManager = FindObjectOfType<ARTrackedImageManager>();
                if (trackedImageManager == null)
                {
                    Debug.LogWarning("[ARImagePortalTracker] No se encontró ARTrackedImageManager en la escena.");
                }
            }

            // Buscar el gestor de colocación si no fue asignado
            if (placementManager == null)
            {
                placementManager = FindObjectOfType<ARPlacementAndManipulator>();
                if (placementManager == null)
                {
                    Debug.LogWarning("[ARImagePortalTracker] No se encontró ARPlacementAndManipulator en la escena.");
                }
            }
        }

        /// <summary>
        /// Suscribirse al evento de cambios en imágenes rastreadas.
        /// </summary>
        private void OnEnable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
            }
        }

        /// <summary>
        /// Desuscribirse del evento y limpiar todos los portales generados.
        /// </summary>
        private void OnDisable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            }

            // Destruir todos los portales generados
            foreach (KeyValuePair<TrackableId, GameObject> kvp in spawnedPortals)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            spawnedPortals.Clear();
            bounceCooldowns.Clear();
        }

        /// <summary>
        /// Actualización de física: aplica efectos de vórtice o trampolín a los objetos
        /// cercanos a cada portal activo.
        /// </summary>
        private void FixedUpdate()
        {
            if (placementManager == null)
            {
                return;
            }

            List<ManipulableObject> spawnedObjects = placementManager.SpawnedObjects;
            if (spawnedObjects == null || spawnedObjects.Count == 0)
            {
                return;
            }

            // Iterar sobre cada portal activo
            foreach (KeyValuePair<TrackableId, GameObject> kvp in spawnedPortals)
            {
                GameObject portal = kvp.Value;
                if (portal == null || !portal.activeInHierarchy)
                {
                    continue;
                }

                Vector3 portalPosition = portal.transform.position;

                if (CurrentPortalMode == PortalMode.Vortex)
                {
                    // Modo Vórtice: atraer y encoger objetos cercanos
                    ApplyVortexEffect(spawnedObjects, portalPosition);
                }
                else
                {
                    // Modo Trampolín: rebotar objetos muy cercanos
                    ApplyTrampolineEffect(spawnedObjects, portalPosition);
                }
            }
        }

        /// <summary>
        /// Actualización por frame: rota los portales para efecto visual de giro.
        /// </summary>
        private void Update()
        {
            // Animar rotación de portales
            foreach (KeyValuePair<TrackableId, GameObject> kvp in spawnedPortals)
            {
                GameObject portal = kvp.Value;
                if (portal != null && portal.activeInHierarchy)
                {
                    portal.transform.Rotate(Vector3.up, PortalSpinSpeed * Time.deltaTime, Space.Self);
                }
            }
        }

        #endregion

        #region Gestión de Imágenes Rastreadas

        /// <summary>
        /// Callback invocado cuando cambian las imágenes rastreadas por ARFoundation.
        /// Gestiona la creación, actualización y eliminación de portales.
        /// </summary>
        /// <param name="eventArgs">Argumentos del evento con listas de imágenes añadidas, actualizadas y eliminadas.</param>
        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
        {
            // Procesar imágenes recién detectadas
            if (eventArgs.added != null)
            {
                for (int i = 0; i < eventArgs.added.Count; i++)
                {
                    ARTrackedImage trackedImage = eventArgs.added[i];
                    if (trackedImage == null)
                    {
                        continue;
                    }

                    if (!spawnedPortals.ContainsKey(trackedImage.trackableId))
                    {
                        GameObject portalVisual = CreatePortalVisual(trackedImage);
                        if (portalVisual != null)
                        {
                            spawnedPortals[trackedImage.trackableId] = portalVisual;
                        }
                    }
                }
            }

            // Procesar imágenes actualizadas (posición/estado de rastreo)
            if (eventArgs.updated != null)
            {
                for (int i = 0; i < eventArgs.updated.Count; i++)
                {
                    ARTrackedImage trackedImage = eventArgs.updated[i];
                    if (trackedImage == null)
                    {
                        continue;
                    }

                    if (spawnedPortals.TryGetValue(trackedImage.trackableId, out GameObject portal))
                    {
                        if (portal == null)
                        {
                            continue;
                        }

                        // Activar o desactivar el portal según el estado de rastreo
                        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;
                        portal.SetActive(isTracking);

                        // Actualizar posición y rotación del portal
                        if (isTracking)
                        {
                            portal.transform.position = trackedImage.transform.position;
                            portal.transform.rotation = trackedImage.transform.rotation;
                        }
                    }
                }
            }

            // Procesar imágenes eliminadas
            if (eventArgs.removed != null)
            {
                for (int i = 0; i < eventArgs.removed.Count; i++)
                {
                    ARTrackedImage trackedImage = eventArgs.removed[i];
                    if (trackedImage == null)
                    {
                        continue;
                    }

                    if (spawnedPortals.TryGetValue(trackedImage.trackableId, out GameObject portal))
                    {
                        if (portal != null)
                        {
                            Destroy(portal);
                        }
                        spawnedPortals.Remove(trackedImage.trackableId);
                    }
                }
            }
        }

        #endregion

        #region Creación de Portales

        /// <summary>
        /// Crea el visual del portal en la posición de la imagen rastreada.
        /// Si hay un prefab asignado, lo instancia. De lo contrario, crea un
        /// cilindro plano con material emisivo según el modo actual.
        /// </summary>
        /// <param name="trackedImage">Imagen rastreada donde se posicionará el portal.</param>
        /// <returns>El GameObject del portal creado, o null si falla.</returns>
        private GameObject CreatePortalVisual(ARTrackedImage trackedImage)
        {
            if (trackedImage == null)
            {
                return null;
            }

            GameObject portal;

            if (portalPrefab != null)
            {
                // Instanciar desde prefab
                portal = Instantiate(portalPrefab, trackedImage.transform.position, trackedImage.transform.rotation);
            }
            else
            {
                // Crear cilindro plano programáticamente como representación visual del portal
                portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                portal.name = "Portal_" + CurrentPortalMode.ToString();
                portal.transform.position = trackedImage.transform.position;
                portal.transform.rotation = trackedImage.transform.rotation;
                portal.transform.localScale = new Vector3(0.3f, 0.01f, 0.3f);

                // Configurar material emisivo según el modo
                Renderer portalRenderer = portal.GetComponent<Renderer>();
                if (portalRenderer != null)
                {
                    Material portalMaterial = new Material(Shader.Find("Standard"));

                    if (CurrentPortalMode == PortalMode.Vortex)
                    {
                        // Color púrpura oscuro/negro para vórtice
                        Color vortexColor = new Color(0.2f, 0.0f, 0.3f, 1f);
                        portalMaterial.color = vortexColor;
                        portalMaterial.SetColor("_EmissionColor", vortexColor * 2f);
                        portalMaterial.EnableKeyword("_EMISSION");
                    }
                    else
                    {
                        // Color verde brillante para trampolín
                        Color trampolineColor = new Color(0.0f, 1.0f, 0.2f, 1f);
                        portalMaterial.color = trampolineColor;
                        portalMaterial.SetColor("_EmissionColor", trampolineColor * 2f);
                        portalMaterial.EnableKeyword("_EMISSION");
                    }

                    portalRenderer.material = portalMaterial;
                }

                // Configurar el collider como trigger para detección de colisiones
                Collider portalCollider = portal.GetComponent<Collider>();
                if (portalCollider != null)
                {
                    portalCollider.isTrigger = true;
                }
            }

            // Parentear al transform de la imagen rastreada
            portal.transform.SetParent(trackedImage.transform);

            return portal;
        }

        /// <summary>
        /// Actualiza el aspecto visual de todos los portales existentes según el modo actual.
        /// Cambia colores del material: púrpura para Vórtice, verde para Trampolín.
        /// </summary>
        private void UpdatePortalVisuals()
        {
            foreach (KeyValuePair<TrackableId, GameObject> kvp in spawnedPortals)
            {
                GameObject portal = kvp.Value;
                if (portal == null)
                {
                    continue;
                }

                Renderer portalRenderer = portal.GetComponent<Renderer>();
                if (portalRenderer == null)
                {
                    continue;
                }

                // Si usa un prefab, no alterar el material
                if (portalPrefab != null)
                {
                    continue;
                }

                Material mat = portalRenderer.material;
                if (mat == null)
                {
                    continue;
                }

                if (CurrentPortalMode == PortalMode.Vortex)
                {
                    Color vortexColor = new Color(0.2f, 0.0f, 0.3f, 1f);
                    mat.color = vortexColor;
                    mat.SetColor("_EmissionColor", vortexColor * 2f);
                }
                else
                {
                    Color trampolineColor = new Color(0.0f, 1.0f, 0.2f, 1f);
                    mat.color = trampolineColor;
                    mat.SetColor("_EmissionColor", trampolineColor * 2f);
                }

                portal.name = "Portal_" + CurrentPortalMode.ToString();
            }
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Alterna entre los modos de portal Vórtice y Trampolín.
        /// Actualiza los visuales de todos los portales existentes.
        /// </summary>
        public void TogglePortalMode()
        {
            if (CurrentPortalMode == PortalMode.Vortex)
            {
                CurrentPortalMode = PortalMode.Trampoline;
            }
            else
            {
                CurrentPortalMode = PortalMode.Vortex;
            }

            // Limpiar enfriamientos de rebote al cambiar de modo
            bounceCooldowns.Clear();

            // Actualizar aspecto visual de portales existentes
            UpdatePortalVisuals();
        }

        #endregion

        #region Efectos de Física

        /// <summary>
        /// Aplica el efecto de vórtice: atrae objetos cercanos hacia el centro del portal
        /// y los encoge progresivamente. Si un objeto se encoge demasiado, es destruido.
        /// </summary>
        /// <param name="spawnedObjects">Lista de objetos manipulables en la escena.</param>
        /// <param name="portalPosition">Posición del centro del portal.</param>
        private void ApplyVortexEffect(List<ManipulableObject> spawnedObjects, Vector3 portalPosition)
        {
            // Iterar en reversa para poder eliminar objetos de forma segura
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                ManipulableObject obj = spawnedObjects[i];
                if (obj == null)
                {
                    continue;
                }

                Rigidbody rb = obj.Rb;
                if (rb == null)
                {
                    continue;
                }

                Vector3 objectPosition = obj.transform.position;
                Vector3 direction = portalPosition - objectPosition;
                float distance = direction.magnitude;

                // Solo afectar objetos dentro del rango del vórtice
                if (distance > vortexRange || distance < 0.001f)
                {
                    continue;
                }

                // Desactivar cinemático para que reciba fuerzas
                obj.SetKinematic(false);

                // Aplicar fuerza de atracción inversamente proporcional a la distancia
                Vector3 attractionForce = direction.normalized * (vortexForce / distance);
                rb.AddForce(attractionForce, ForceMode.Acceleration);

                // Encoger el objeto progresivamente
                Vector3 currentScale = obj.transform.localScale;
                obj.transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, shrinkRate * Time.fixedDeltaTime);

                // Si el objeto es demasiado pequeño, destruirlo
                if (obj.transform.localScale.x < MinScaleBeforeDestroy &&
                    obj.transform.localScale.y < MinScaleBeforeDestroy &&
                    obj.transform.localScale.z < MinScaleBeforeDestroy)
                {
                    Destroy(obj.gameObject);
                }
            }
        }

        /// <summary>
        /// Aplica el efecto de trampolín: cuando un objeto se acerca lo suficiente al portal,
        /// recibe un impulso hacia arriba. Incluye sistema de enfriamiento para evitar
        /// rebotes repetidos cada frame.
        /// </summary>
        /// <param name="spawnedObjects">Lista de objetos manipulables en la escena.</param>
        /// <param name="portalPosition">Posición del centro del portal.</param>
        private void ApplyTrampolineEffect(List<ManipulableObject> spawnedObjects, Vector3 portalPosition)
        {
            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                ManipulableObject obj = spawnedObjects[i];
                if (obj == null)
                {
                    continue;
                }

                Rigidbody rb = obj.Rb;
                if (rb == null)
                {
                    continue;
                }

                int objId = obj.GetInstanceID();
                float distance = Vector3.Distance(obj.transform.position, portalPosition);

                // Verificar si el objeto está dentro del rango de rebote
                if (distance > TrampolineBounceRange)
                {
                    continue;
                }

                // Verificar enfriamiento de rebote para este objeto
                if (bounceCooldowns.TryGetValue(objId, out float cooldownEnd))
                {
                    if (Time.time < cooldownEnd)
                    {
                        continue;
                    }
                }

                // Desactivar cinemático para que reciba fuerzas
                obj.SetKinematic(false);

                // Aplicar impulso hacia arriba
                rb.AddForce(Vector3.up * trampolineForce, ForceMode.Impulse);

                // Registrar enfriamiento para evitar rebotes consecutivos
                bounceCooldowns[objId] = Time.time + TrampBounceCooldown;
            }
        }

        #endregion
    }
}
