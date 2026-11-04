// ============================================================
// ARPlacementAndManipulator.cs
// Gestiona el posicionamiento del retículo sobre planos AR,
// la generación de objetos, la selección mediante Physics.Raycast,
// y la manipulación de arrastre/movimiento a lo largo de planos.
// También actualiza los colliders de los planos detectados.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;

namespace ARSandbox
{
    /// <summary>
    /// Controlador principal de colocación y manipulación de objetos
    /// en el sandbox AR. Gestiona el retículo, la generación de objetos
    /// sobre planos detectados, la selección por toque y el arrastre.
    /// </summary>
    public class ARPlacementAndManipulator : MonoBehaviour
    {
        // ── Eventos estáticos ───────────────────────────────────────────

        /// <summary>
        /// Evento disparado cuando se genera un nuevo objeto en la escena.
        /// </summary>
        public static event Action<ManipulableObject> OnObjectSpawned;

        /// <summary>
        /// Evento disparado cuando se selecciona un objeto.
        /// </summary>
        public static event Action<ManipulableObject> OnObjectSelected;

        /// <summary>
        /// Evento disparado cuando se deselecciona el objeto actual.
        /// </summary>
        public static event Action OnObjectDeselected;

        // ── Campos serializados ─────────────────────────────────────────

        [Header("Referencias AR")]
        [SerializeField] private ARRaycastManager arRaycastManager;
        [SerializeField] private ARPlaneManager arPlaneManager;

        [Header("Prefabs y configuración de generación")]
        [SerializeField] private List<GameObject> spawnablePrefabs;
        [SerializeField] private GameObject reticlePrefab;
        [SerializeField] private Transform spawnedObjectsContainer;
        [SerializeField] private int maxSpawnedObjects = 50;

        // ── Propiedades públicas ────────────────────────────────────────

        /// <summary>
        /// Objeto actualmente seleccionado (null si no hay selección).
        /// </summary>
        public ManipulableObject SelectedObject { get; private set; }

        /// <summary>
        /// Cantidad de objetos generados actualmente en la escena.
        /// </summary>
        public int SpawnedObjectCount => spawnedObjects.Count;

        /// <summary>
        /// Lista de todos los objetos manipulables generados.
        /// </summary>
        public List<ManipulableObject> SpawnedObjects => spawnedObjects;

        // ── Campos privados ─────────────────────────────────────────────

        /// <summary>
        /// Instancia del retículo en la escena.
        /// </summary>
        private GameObject reticleInstance;

        /// <summary>
        /// Lista interna de objetos manipulables generados.
        /// </summary>
        private List<ManipulableObject> spawnedObjects = new List<ManipulableObject>();

        /// <summary>
        /// Índice del prefab actualmente seleccionado para generar.
        /// </summary>
        private int selectedPrefabIndex = 0;

        /// <summary>
        /// Indica si el usuario está arrastrando un objeto.
        /// </summary>
        private bool isDragging = false;

        /// <summary>
        /// Lista estática reutilizable para resultados de AR raycast.
        /// </summary>
        private static List<ARRaycastHit> arHits = new List<ARRaycastHit>();

        /// <summary>
        /// Velocidad de interpolación para el movimiento suave al arrastrar.
        /// </summary>
        private const float DragLerpSpeed = 12f;

        // ═════════════════════════════════════════════════════════════════
        // Ciclo de vida de Unity
        // ═════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Intentar encontrar los managers automáticamente si no fueron
            // asignados desde el Inspector.
            if (arRaycastManager == null)
            {
                arRaycastManager = FindObjectOfType<ARRaycastManager>();
                if (arRaycastManager != null)
                {
                    Debug.Log("[ARPlacementAndManipulator] ARRaycastManager encontrado automáticamente.");
                }
                else
                {
                    Debug.LogWarning("[ARPlacementAndManipulator] No se encontró ARRaycastManager. La colocación de objetos no funcionará.");
                }
            }

            if (arPlaneManager == null)
            {
                arPlaneManager = FindObjectOfType<ARPlaneManager>();
                if (arPlaneManager != null)
                {
                    Debug.Log("[ARPlacementAndManipulator] ARPlaneManager encontrado automáticamente.");
                }
                else
                {
                    Debug.LogWarning("[ARPlacementAndManipulator] No se encontró ARPlaneManager. La actualización de colliders de planos no funcionará.");
                }
            }

            // Instanciar el retículo si se proporcionó un prefab.
            if (reticlePrefab != null)
            {
                reticleInstance = Instantiate(reticlePrefab);
                reticleInstance.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[ARPlacementAndManipulator] No se asignó reticlePrefab. No se mostrará indicador de posición.");
            }

            // Inicializar la lista de prefabs si es nula.
            if (spawnablePrefabs == null)
            {
                spawnablePrefabs = new List<GameObject>();
            }
        }

        private void OnEnable()
        {
            // Suscribirse al evento de cambios en planos detectados.
            if (arPlaneManager != null)
            {
                arPlaneManager.planesChanged += OnPlanesChanged;
            }

            // Suscribirse al evento de destrucción de objetos manipulables.
            ManipulableObject.OnDestroyed += OnObjectWasDestroyed;
        }

        private void OnDisable()
        {
            // Desuscribirse de los eventos para evitar fugas de memoria.
            if (arPlaneManager != null)
            {
                arPlaneManager.planesChanged -= OnPlanesChanged;
            }

            ManipulableObject.OnDestroyed -= OnObjectWasDestroyed;
        }

        private void Update()
        {
            // Paso 1: Actualizar la posición del retículo.
            UpdateReticle();

            // Paso 2: Procesar la entrada táctil o de ratón (Editor/Móvil).
            HandleInput();
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos públicos
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Establece el índice del prefab seleccionado para la siguiente
        /// generación de objeto. Valida que el índice sea válido.
        /// </summary>
        /// <param name="index">Índice del prefab en la lista spawnablePrefabs.</param>
        public void SetSelectedPrefabIndex(int index)
        {
            if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
            {
                Debug.LogWarning("[ARPlacementAndManipulator] La lista de prefabs está vacía. No se puede establecer el índice.");
                return;
            }

            if (index < 0 || index >= spawnablePrefabs.Count)
            {
                Debug.LogWarning($"[ARPlacementAndManipulator] Índice {index} fuera de rango. Rango válido: [0, {spawnablePrefabs.Count - 1}].");
                return;
            }

            selectedPrefabIndex = index;
        }

        /// <summary>
        /// Destruye todos los objetos generados y limpia la lista.
        /// </summary>
        public void ClearAllObjects()
        {
            // Deseleccionar el objeto actual si hay uno seleccionado.
            if (SelectedObject != null)
            {
                DeselectObject();
            }

            // Destruir todos los objetos de la lista.
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] != null && spawnedObjects[i].gameObject != null)
                {
                    Destroy(spawnedObjects[i].gameObject);
                }
            }

            spawnedObjects.Clear();
            Debug.Log("[ARPlacementAndManipulator] Todos los objetos han sido eliminados.");
        }

        /// <summary>
        /// Deselecciona el objeto actualmente seleccionado.
        /// </summary>
        public void DeselectObject()
        {
            if (SelectedObject != null)
            {
                SelectedObject.SetSelected(false);
                SelectedObject.SetKinematic(false);
                SelectedObject = null;
            }

            isDragging = false;
            OnObjectDeselected?.Invoke();
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados - Retículo y Posicionamiento
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtiene la pose de colocación para una posición de pantalla dada.
        /// Soporta detección oficial de AR Foundation en dispositivos y fallback
        /// a física de colliders / suelo virtual en el Editor de Unity.
        /// </summary>
        private bool TryGetPlacementPose(Vector2 screenPosition, out Pose pose)
        {
            // 1. Detección AR oficial (dispositivo real)
            if (arRaycastManager != null && arRaycastManager.Raycast(screenPosition, arHits, TrackableType.PlaneWithinPolygon))
            {
                pose = arHits[0].pose;
                return true;
            }

            // 2. Fallback de prueba para Editor / PC: raycast contra colliders de la escena (mesa, suelo o plano de prueba)
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(screenPosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // Si golpea una superficie que no sea un objeto manipulable
                    if (hit.collider.GetComponentInParent<ManipulableObject>() == null)
                    {
                        pose = new Pose(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
                        return true;
                    }
                }

                // 3. Fallback virtual a nivel del suelo (y = 0 o ligeramente debajo de la cámara)
                Plane virtualGround = new Plane(Vector3.up, Vector3.zero);
                if (virtualGround.Raycast(ray, out float enter) && enter > 0f && enter < 50f)
                {
                    pose = new Pose(ray.GetPoint(enter), Quaternion.identity);
                    return true;
                }
            }

            pose = default;
            return false;
        }

        /// <summary>
        /// Actualiza la posición del retículo realizando un raycast
        /// desde el centro de la pantalla.
        /// </summary>
        private void UpdateReticle()
        {
            if (reticleInstance == null)
            {
                return;
            }

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (TryGetPlacementPose(screenCenter, out Pose hitPose))
            {
                reticleInstance.transform.position = hitPose.position;
                reticleInstance.transform.rotation = hitPose.rotation;

                if (!reticleInstance.activeSelf)
                {
                    reticleInstance.SetActive(true);
                }
            }
            else
            {
                if (reticleInstance.activeSelf)
                {
                    reticleInstance.SetActive(false);
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados - Entrada Táctil y Ratón
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Procesa la entrada tanto táctil (móvil) como de ratón (Editor/PC).
        /// </summary>
        private void HandleInput()
        {
            // Entrada táctil en móvil
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    return;
                }

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        HandlePointerBegan(touch.position);
                        break;
                    case TouchPhase.Moved:
                        HandlePointerMoved(touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        HandlePointerEnded();
                        break;
                }
            }
            // Soporte de ratón para pruebas en el Editor de Unity
            else
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    HandlePointerBegan(Input.mousePosition);
                }
                else if (Input.GetMouseButton(0) && isDragging)
                {
                    HandlePointerMoved(Input.mousePosition);
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    HandlePointerEnded();
                }
            }
        }

        /// <summary>
        /// Maneja el inicio de un toque o clic de ratón.
        /// </summary>
        private void HandlePointerBegan(Vector2 screenPosition)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // 1. Intentar seleccionar un objeto existente con Physics.Raycast
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                ManipulableObject manipulable = hit.collider.GetComponentInParent<ManipulableObject>();
                if (manipulable != null)
                {
                    SelectObject(manipulable);
                    isDragging = true;
                    return;
                }
            }

            // 2. Si no seleccionó un objeto existente, intentar generar uno nuevo
            if (reticleInstance != null && reticleInstance.activeSelf)
            {
                if (TryGetPlacementPose(screenPosition, out Pose spawnPose))
                {
                    SpawnObject(spawnPose);
                }
            }
        }

        /// <summary>
        /// Maneja el movimiento continuo para arrastrar el objeto seleccionado.
        /// </summary>
        private void HandlePointerMoved(Vector2 screenPosition)
        {
            if (!isDragging || SelectedObject == null)
            {
                return;
            }

            if (TryGetPlacementPose(screenPosition, out Pose hitPose))
            {
                SelectedObject.transform.position = Vector3.Lerp(
                    SelectedObject.transform.position,
                    hitPose.position,
                    Time.deltaTime * DragLerpSpeed
                );
            }
        }

        /// <summary>
        /// Maneja el final del puntero. Detiene el arrastre.
        /// </summary>
        private void HandlePointerEnded()
        {
            isDragging = false;
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados - Selección
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Selecciona un objeto manipulable, deseleccionando el anterior
        /// si hay uno seleccionado.
        /// </summary>
        /// <param name="obj">Objeto a seleccionar.</param>
        private void SelectObject(ManipulableObject obj)
        {
            if (obj == null)
            {
                return;
            }

            // Si ya hay un objeto seleccionado, deseleccionarlo primero.
            if (SelectedObject != null && SelectedObject != obj)
            {
                SelectedObject.SetSelected(false);
                SelectedObject.SetKinematic(false);
            }

            // Seleccionar el nuevo objeto.
            SelectedObject = obj;
            SelectedObject.SetSelected(true);
            SelectedObject.SetKinematic(true);

            // Disparar el evento de selección.
            OnObjectSelected?.Invoke(SelectedObject);
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados - Generación de objetos
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Genera un nuevo objeto en la posición y rotación indicadas.
        /// Agrega el componente ManipulableObject si no lo tiene, lo
        /// añade a la lista de seguimiento y lo selecciona.
        /// </summary>
        /// <param name="pose">Posición y rotación donde generar el objeto.</param>
        private void SpawnObject(Pose pose)
        {
            // Verificar que no se haya alcanzado el límite de objetos.
            if (spawnedObjects.Count >= maxSpawnedObjects)
            {
                Debug.LogWarning($"[ARPlacementAndManipulator] Se alcanzó el límite máximo de objetos ({maxSpawnedObjects}). No se puede generar más.");
                return;
            }

            // Verificar que haya prefabs disponibles.
            if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
            {
                Debug.LogWarning("[ARPlacementAndManipulator] No hay prefabs configurados para generar.");
                return;
            }

            // Validar el índice del prefab seleccionado.
            if (selectedPrefabIndex < 0 || selectedPrefabIndex >= spawnablePrefabs.Count)
            {
                Debug.LogWarning($"[ARPlacementAndManipulator] Índice de prefab inválido: {selectedPrefabIndex}.");
                return;
            }

            // Verificar que el prefab no sea nulo.
            GameObject prefab = spawnablePrefabs[selectedPrefabIndex];
            if (prefab == null)
            {
                Debug.LogWarning($"[ARPlacementAndManipulator] El prefab en el índice {selectedPrefabIndex} es nulo.");
                return;
            }

            // Instanciar el objeto en la posición del retículo.
            GameObject spawnedGO = Instantiate(prefab, pose.position, pose.rotation);

            // Asignar el contenedor padre si existe.
            if (spawnedObjectsContainer != null)
            {
                spawnedGO.transform.SetParent(spawnedObjectsContainer, true);
            }

            // Agregar el componente ManipulableObject si no lo tiene ya.
            ManipulableObject manipulable = spawnedGO.GetComponent<ManipulableObject>();
            if (manipulable == null)
            {
                manipulable = spawnedGO.AddComponent<ManipulableObject>();
            }

            // Añadir a la lista de seguimiento.
            spawnedObjects.Add(manipulable);

            // Disparar el evento de generación.
            OnObjectSpawned?.Invoke(manipulable);

            // Seleccionar el objeto recién generado.
            SelectObject(manipulable);

            Debug.Log($"[ARPlacementAndManipulator] Objeto '{spawnedGO.name}' generado. Total: {spawnedObjects.Count}");
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados - Eventos
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Callback ejecutado cuando se detectan cambios en los planos AR.
        /// Agrega o actualiza los BoxCollider de los planos para permitir
        /// la interacción física con los objetos.
        /// </summary>
        /// <param name="args">Datos del evento con planos añadidos, actualizados y eliminados.</param>
        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            // Procesar planos añadidos.
            if (args.added != null)
            {
                for (int i = 0; i < args.added.Count; i++)
                {
                    UpdatePlaneCollider(args.added[i]);
                }
            }

            // Procesar planos actualizados.
            if (args.updated != null)
            {
                for (int i = 0; i < args.updated.Count; i++)
                {
                    UpdatePlaneCollider(args.updated[i]);
                }
            }
        }

        /// <summary>
        /// Agrega o actualiza un BoxCollider en un plano AR para que
        /// coincida con las dimensiones del plano detectado.
        /// </summary>
        /// <param name="plane">Plano AR al que se le agregará/actualizará el collider.</param>
        private void UpdatePlaneCollider(ARPlane plane)
        {
            if (plane == null)
            {
                return;
            }

            // Obtener o crear el BoxCollider del plano.
            BoxCollider boxCollider = plane.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = plane.gameObject.AddComponent<BoxCollider>();
            }

            // Ajustar el tamaño del collider a las dimensiones del plano.
            // Las extensiones del plano representan la mitad del tamaño,
            // por lo que se multiplican por 2 para obtener el tamaño completo.
            Vector2 planeExtents = plane.extents;
            boxCollider.size = new Vector3(
                planeExtents.x * 2f,
                0.02f,
                planeExtents.y * 2f
            );

            // Centrar el collider en el plano.
            boxCollider.center = Vector3.zero;
        }

        /// <summary>
        /// Callback ejecutado cuando un ManipulableObject es destruido.
        /// Lo elimina de la lista de seguimiento y limpia la selección
        /// si era el objeto seleccionado.
        /// </summary>
        /// <param name="obj">Objeto que fue destruido.</param>
        private void OnObjectWasDestroyed(ManipulableObject obj)
        {
            if (obj == null)
            {
                return;
            }

            // Eliminar de la lista de seguimiento.
            spawnedObjects.Remove(obj);

            // Si era el objeto seleccionado, limpiar la selección.
            if (SelectedObject == obj)
            {
                SelectedObject = null;
                isDragging = false;
                OnObjectDeselected?.Invoke();
            }
        }
    }
}
