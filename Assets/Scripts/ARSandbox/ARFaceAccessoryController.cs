using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARSandbox
{
    /// <summary>
    /// Controlador de rastreo facial AR, colocación de accesorios y efecto láser ocular.
    /// Gestiona la detección de rostros, instancia accesorios decorativos y permite
    /// disparar láseres desde los ojos del rostro rastreado.
    /// </summary>
    public class ARFaceAccessoryController : MonoBehaviour
    {
        #region Campos Serializados

        [Header("Referencias AR")]
        [SerializeField, Tooltip("Gestor de rostros de ARFoundation.")]
        private ARFaceManager faceManager;

        [Header("Accesorios")]
        [SerializeField, Tooltip("Lista de prefabs de accesorios. Si está vacía, se crean accesorios por defecto.")]
        private List<GameObject> accessoryPrefabs;

        [Header("Láser Ocular")]
        [SerializeField, Tooltip("Duración del efecto láser en segundos.")]
        private float laserDuration = 0.5f;

        [SerializeField, Tooltip("Longitud del rayo láser.")]
        private float laserLength = 3f;

        [SerializeField, Tooltip("Color del rayo láser.")]
        private Color laserColor = Color.red;

        #endregion

        #region Campos Privados

        /// <summary>
        /// Índice del accesorio actualmente seleccionado en la lista de prefabs.
        /// </summary>
        private int currentAccessoryIndex = 0;

        /// <summary>
        /// Diccionario que mapea el ID rastreable de cada rostro a su accesorio instanciado.
        /// </summary>
        private Dictionary<TrackableId, GameObject> spawnedAccessories = new Dictionary<TrackableId, GameObject>();

        /// <summary>
        /// Diccionario que mapea el ID rastreable de cada rostro a sus LineRenderers de láser (2 por rostro).
        /// </summary>
        private Dictionary<TrackableId, LineRenderer[]> faceLaserRenderers = new Dictionary<TrackableId, LineRenderer[]>();

        /// <summary>
        /// Referencia al rostro actualmente rastreado (el primero detectado).
        /// </summary>
        private ARFace currentFace;

        /// <summary>
        /// Temporizador restante del efecto láser.
        /// </summary>
        private float laserTimer = 0f;

        /// <summary>
        /// Posición local del ojo izquierdo relativa al rostro (aproximada).
        /// </summary>
        private static readonly Vector3 LeftEyeLocalPosition = new Vector3(-0.03f, 0.02f, 0.06f);

        /// <summary>
        /// Posición local del ojo derecho relativa al rostro (aproximada).
        /// </summary>
        private static readonly Vector3 RightEyeLocalPosition = new Vector3(0.03f, 0.02f, 0.06f);

        /// <summary>
        /// Posición local de la frente relativa al rostro para accesorios (aproximada).
        /// </summary>
        private static readonly Vector3 ForeheadLocalPosition = new Vector3(0f, 0.08f, 0.05f);

        /// <summary>
        /// Lista de accesorios por defecto creados programáticamente.
        /// </summary>
        private List<GameObject> defaultAccessoryTemplates = new List<GameObject>();

        /// <summary>
        /// Ancho del rayo láser (LineRenderer).
        /// </summary>
        private const float LaserWidth = 0.01f;

        #endregion

        #region Ciclo de Vida de Unity

        /// <summary>
        /// Inicialización temprana: busca el gestor de rostros si no fue asignado.
        /// </summary>
        private void Awake()
        {
            if (faceManager == null)
            {
                faceManager = FindObjectOfType<ARFaceManager>();
                if (faceManager == null)
                {
                    Debug.LogWarning("[ARFaceAccessoryController] No se encontró ARFaceManager en la escena.");
                }
            }

            // Inicializar la lista si es null
            if (accessoryPrefabs == null)
            {
                accessoryPrefabs = new List<GameObject>();
            }
        }

        /// <summary>
        /// Inicio: si la lista de accesorios está vacía, crear accesorios por defecto.
        /// </summary>
        private void Start()
        {
            if (accessoryPrefabs.Count == 0)
            {
                CreateDefaultAccessories();
            }
        }

        /// <summary>
        /// Suscribirse al evento de cambios en rostros rastreados.
        /// </summary>
        private void OnEnable()
        {
            if (faceManager != null)
            {
                faceManager.facesChanged += OnFacesChanged;
            }
        }

        /// <summary>
        /// Desuscribirse del evento y limpiar todos los accesorios y láseres.
        /// </summary>
        private void OnDisable()
        {
            if (faceManager != null)
            {
                faceManager.facesChanged -= OnFacesChanged;
            }

            // Destruir todos los accesorios generados
            foreach (KeyValuePair<TrackableId, GameObject> kvp in spawnedAccessories)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            spawnedAccessories.Clear();

            // Destruir todos los objetos de láser
            foreach (KeyValuePair<TrackableId, LineRenderer[]> kvp in faceLaserRenderers)
            {
                LineRenderer[] renderers = kvp.Value;
                if (renderers == null)
                {
                    continue;
                }
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        Destroy(renderers[i].gameObject);
                    }
                }
            }
            faceLaserRenderers.Clear();

            // Destruir plantillas de accesorios por defecto
            for (int i = 0; i < defaultAccessoryTemplates.Count; i++)
            {
                if (defaultAccessoryTemplates[i] != null)
                {
                    Destroy(defaultAccessoryTemplates[i]);
                }
            }
            defaultAccessoryTemplates.Clear();

            currentFace = null;
        }

        /// <summary>
        /// Actualización por frame: gestiona el temporizador del láser y actualiza
        /// las posiciones de los rayos activos.
        /// </summary>
        private void Update()
        {
            if (laserTimer > 0f)
            {
                laserTimer -= Time.deltaTime;

                // Actualizar posiciones del láser mientras el rostro se mueve
                UpdateActiveLaserPositions();

                // Desactivar los láseres cuando el temporizador expire
                if (laserTimer <= 0f)
                {
                    DisableAllLasers();
                }
            }
        }

        #endregion

        #region Gestión de Rostros

        /// <summary>
        /// Callback invocado cuando cambian los rostros rastreados por ARFoundation.
        /// Gestiona la creación, actualización y eliminación de accesorios faciales.
        /// </summary>
        /// <param name="eventArgs">Argumentos del evento con listas de rostros añadidos, actualizados y eliminados.</param>
        private void OnFacesChanged(ARFacesChangedEventArgs eventArgs)
        {
            // Procesar rostros recién detectados
            if (eventArgs.added != null)
            {
                for (int i = 0; i < eventArgs.added.Count; i++)
                {
                    ARFace face = eventArgs.added[i];
                    if (face == null)
                    {
                        continue;
                    }

                    // Almacenar referencia al primer rostro detectado
                    if (currentFace == null)
                    {
                        currentFace = face;
                    }

                    // Instanciar accesorio como hijo del rostro
                    if (!spawnedAccessories.ContainsKey(face.trackableId))
                    {
                        GameObject accessory = InstantiateAccessory(face);
                        if (accessory != null)
                        {
                            spawnedAccessories[face.trackableId] = accessory;
                        }
                    }

                    // Crear LineRenderers para los láseres (2 por rostro, uno por ojo)
                    if (!faceLaserRenderers.ContainsKey(face.trackableId))
                    {
                        LineRenderer[] lasers = CreateLaserRenderers(face);
                        if (lasers != null)
                        {
                            faceLaserRenderers[face.trackableId] = lasers;
                        }
                    }
                }
            }

            // Procesar rostros actualizados (los accesorios parenteados se actualizan automáticamente)
            if (eventArgs.updated != null)
            {
                for (int i = 0; i < eventArgs.updated.Count; i++)
                {
                    ARFace face = eventArgs.updated[i];
                    if (face == null)
                    {
                        continue;
                    }

                    // Actualizar la referencia al rostro actual si es el mismo
                    if (currentFace != null && face.trackableId == currentFace.trackableId)
                    {
                        currentFace = face;
                    }
                }
            }

            // Procesar rostros eliminados
            if (eventArgs.removed != null)
            {
                for (int i = 0; i < eventArgs.removed.Count; i++)
                {
                    ARFace face = eventArgs.removed[i];
                    if (face == null)
                    {
                        continue;
                    }

                    // Destruir accesorio asociado
                    if (spawnedAccessories.TryGetValue(face.trackableId, out GameObject accessory))
                    {
                        if (accessory != null)
                        {
                            Destroy(accessory);
                        }
                        spawnedAccessories.Remove(face.trackableId);
                    }

                    // Destruir LineRenderers de láser asociados
                    if (faceLaserRenderers.TryGetValue(face.trackableId, out LineRenderer[] lasers))
                    {
                        if (lasers != null)
                        {
                            for (int j = 0; j < lasers.Length; j++)
                            {
                                if (lasers[j] != null)
                                {
                                    Destroy(lasers[j].gameObject);
                                }
                            }
                        }
                        faceLaserRenderers.Remove(face.trackableId);
                    }

                    // Limpiar referencia al rostro actual si fue eliminado
                    if (currentFace != null && face.trackableId == currentFace.trackableId)
                    {
                        currentFace = null;
                    }
                }
            }
        }

        #endregion

        #region Creación de Accesorios

        /// <summary>
        /// Instancia el accesorio actual como hijo del rostro detectado,
        /// posicionándolo en el área de la frente.
        /// </summary>
        /// <param name="face">Rostro AR al que se le asignará el accesorio.</param>
        /// <returns>El GameObject del accesorio instanciado, o null si falla.</returns>
        private GameObject InstantiateAccessory(ARFace face)
        {
            if (face == null)
            {
                return null;
            }

            if (accessoryPrefabs == null || accessoryPrefabs.Count == 0)
            {
                Debug.LogWarning("[ARFaceAccessoryController] No hay prefabs de accesorios disponibles.");
                return null;
            }

            // Asegurar que el índice esté en rango
            currentAccessoryIndex = Mathf.Clamp(currentAccessoryIndex, 0, accessoryPrefabs.Count - 1);

            GameObject prefab = accessoryPrefabs[currentAccessoryIndex];
            if (prefab == null)
            {
                Debug.LogWarning("[ARFaceAccessoryController] El prefab del accesorio en el índice " + currentAccessoryIndex + " es nulo.");
                return null;
            }

            // Instanciar el accesorio como hijo del rostro
            GameObject accessory = Instantiate(prefab, face.transform);
            accessory.transform.localPosition = ForeheadLocalPosition;
            accessory.transform.localRotation = Quaternion.identity;
            accessory.name = "Accesorio_Facial_" + face.trackableId.ToString();

            return accessory;
        }

        /// <summary>
        /// Crea dos LineRenderers (uno por ojo) como hijos del rostro para el efecto láser.
        /// Los renderers se crean desactivados y se activan al disparar.
        /// </summary>
        /// <param name="face">Rostro AR al que se le asignarán los láseres.</param>
        /// <returns>Array de 2 LineRenderers, o null si falla.</returns>
        private LineRenderer[] CreateLaserRenderers(ARFace face)
        {
            if (face == null)
            {
                return null;
            }

            LineRenderer[] lasers = new LineRenderer[2];
            string[] eyeNames = { "Laser_OjoIzquierdo", "Laser_OjoDerecho" };
            Vector3[] eyePositions = { LeftEyeLocalPosition, RightEyeLocalPosition };

            for (int i = 0; i < 2; i++)
            {
                // Crear objeto para el láser
                GameObject laserObj = new GameObject(eyeNames[i] + "_" + face.trackableId.ToString());
                laserObj.transform.SetParent(face.transform);
                laserObj.transform.localPosition = eyePositions[i];
                laserObj.transform.localRotation = Quaternion.identity;

                // Configurar LineRenderer
                LineRenderer lr = laserObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = LaserWidth;
                lr.endWidth = LaserWidth;
                lr.useWorldSpace = true;

                // Configurar material del láser
                Material laserMaterial = new Material(Shader.Find("Sprites/Default"));
                laserMaterial.color = laserColor;
                lr.material = laserMaterial;
                lr.startColor = laserColor;
                lr.endColor = laserColor;

                // Iniciar desactivado
                lr.enabled = false;

                lasers[i] = lr;
            }

            return lasers;
        }

        /// <summary>
        /// Crea 3 accesorios por defecto programáticamente cuando no hay prefabs asignados:
        /// 1. Gafas cibernéticas (dos cubos a nivel de ojos con material cyan)
        /// 2. Corona (cilindro amarillo en la parte superior de la cabeza)
        /// 3. Casco (media esfera sobre la cabeza)
        /// Estos objetos se almacenan como plantillas desactivadas para clonar.
        /// </summary>
        private void CreateDefaultAccessories()
        {
            // === Accesorio 1: Gafas Cibernéticas ===
            GameObject cyberGlasses = new GameObject("Plantilla_GafasCiberneticas");
            cyberGlasses.SetActive(false);

            // Lente izquierdo
            GameObject leftLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftLens.name = "Lente_Izquierdo";
            leftLens.transform.SetParent(cyberGlasses.transform);
            leftLens.transform.localPosition = new Vector3(-0.025f, -0.06f, 0.01f);
            leftLens.transform.localScale = new Vector3(0.035f, 0.015f, 0.01f);

            // Aplicar material cyan al lente izquierdo
            Renderer leftRenderer = leftLens.GetComponent<Renderer>();
            if (leftRenderer != null)
            {
                Material cyanMat = new Material(Shader.Find("Standard"));
                cyanMat.color = Color.cyan;
                cyanMat.SetColor("_EmissionColor", Color.cyan * 1.5f);
                cyanMat.EnableKeyword("_EMISSION");
                leftRenderer.material = cyanMat;
            }

            // Eliminar collider innecesario del lente izquierdo
            Collider leftCollider = leftLens.GetComponent<Collider>();
            if (leftCollider != null)
            {
                Destroy(leftCollider);
            }

            // Lente derecho
            GameObject rightLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightLens.name = "Lente_Derecho";
            rightLens.transform.SetParent(cyberGlasses.transform);
            rightLens.transform.localPosition = new Vector3(0.025f, -0.06f, 0.01f);
            rightLens.transform.localScale = new Vector3(0.035f, 0.015f, 0.01f);

            // Aplicar material cyan al lente derecho
            Renderer rightRenderer = rightLens.GetComponent<Renderer>();
            if (rightRenderer != null)
            {
                Material cyanMat = new Material(Shader.Find("Standard"));
                cyanMat.color = Color.cyan;
                cyanMat.SetColor("_EmissionColor", Color.cyan * 1.5f);
                cyanMat.EnableKeyword("_EMISSION");
                rightRenderer.material = cyanMat;
            }

            // Eliminar collider innecesario del lente derecho
            Collider rightCollider = rightLens.GetComponent<Collider>();
            if (rightCollider != null)
            {
                Destroy(rightCollider);
            }

            defaultAccessoryTemplates.Add(cyberGlasses);
            accessoryPrefabs.Add(cyberGlasses);

            // === Accesorio 2: Corona ===
            GameObject crown = new GameObject("Plantilla_Corona");
            crown.SetActive(false);

            GameObject crownMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crownMesh.name = "Malla_Corona";
            crownMesh.transform.SetParent(crown.transform);
            crownMesh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            crownMesh.transform.localScale = new Vector3(0.08f, 0.02f, 0.08f);

            // Aplicar material dorado a la corona
            Renderer crownRenderer = crownMesh.GetComponent<Renderer>();
            if (crownRenderer != null)
            {
                Material goldMat = new Material(Shader.Find("Standard"));
                goldMat.color = Color.yellow;
                goldMat.SetColor("_EmissionColor", Color.yellow * 0.8f);
                goldMat.EnableKeyword("_EMISSION");
                crownRenderer.material = goldMat;
            }

            // Eliminar collider innecesario de la corona
            Collider crownCollider = crownMesh.GetComponent<Collider>();
            if (crownCollider != null)
            {
                Destroy(crownCollider);
            }

            defaultAccessoryTemplates.Add(crown);
            accessoryPrefabs.Add(crown);

            // === Accesorio 3: Casco ===
            GameObject helmet = new GameObject("Plantilla_Casco");
            helmet.SetActive(false);

            GameObject helmetMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            helmetMesh.name = "Malla_Casco";
            helmetMesh.transform.SetParent(helmet.transform);
            helmetMesh.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            // Escalar como media esfera: aplanado en la parte inferior
            helmetMesh.transform.localScale = new Vector3(0.15f, 0.1f, 0.15f);

            // Aplicar material gris metálico al casco
            Renderer helmetRenderer = helmetMesh.GetComponent<Renderer>();
            if (helmetRenderer != null)
            {
                Material metalMat = new Material(Shader.Find("Standard"));
                metalMat.color = new Color(0.6f, 0.6f, 0.7f, 1f);
                metalMat.SetFloat("_Metallic", 0.8f);
                metalMat.SetFloat("_Glossiness", 0.6f);
                helmetRenderer.material = metalMat;
            }

            // Eliminar collider innecesario del casco
            Collider helmetCollider = helmetMesh.GetComponent<Collider>();
            if (helmetCollider != null)
            {
                Destroy(helmetCollider);
            }

            defaultAccessoryTemplates.Add(helmet);
            accessoryPrefabs.Add(helmet);
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Cicla al siguiente accesorio en la lista de prefabs.
        /// Destruye los accesorios actuales y re-instancia con el nuevo prefab
        /// para cada rostro rastreado.
        /// </summary>
        public void CycleAccessory()
        {
            if (accessoryPrefabs == null || accessoryPrefabs.Count == 0)
            {
                Debug.LogWarning("[ARFaceAccessoryController] No hay accesorios disponibles para ciclar.");
                return;
            }

            // Avanzar al siguiente índice de forma circular
            currentAccessoryIndex = (currentAccessoryIndex + 1) % accessoryPrefabs.Count;

            // Recopilar las claves para iterar de forma segura
            List<TrackableId> faceIds = new List<TrackableId>(spawnedAccessories.Keys);

            for (int i = 0; i < faceIds.Count; i++)
            {
                TrackableId faceId = faceIds[i];

                // Destruir accesorio actual
                if (spawnedAccessories.TryGetValue(faceId, out GameObject oldAccessory))
                {
                    if (oldAccessory != null)
                    {
                        Destroy(oldAccessory);
                    }
                    spawnedAccessories.Remove(faceId);
                }

                // Buscar el ARFace correspondiente para re-instanciar
                ARFace targetFace = FindTrackedFaceById(faceId);
                if (targetFace == null)
                {
                    continue;
                }

                // Instanciar nuevo accesorio
                GameObject newAccessory = InstantiateAccessory(targetFace);
                if (newAccessory != null)
                {
                    spawnedAccessories[faceId] = newAccessory;
                }
            }
        }

        /// <summary>
        /// Dispara el efecto láser desde ambos ojos del rostro rastreado actualmente.
        /// Activa los LineRenderers y establece el temporizador de duración.
        /// </summary>
        public void ShootLaser()
        {
            if (currentFace == null)
            {
                Debug.LogWarning("[ARFaceAccessoryController] No hay rostro rastreado para disparar láser.");
                return;
            }

            // Buscar los LineRenderers para el rostro actual
            if (!faceLaserRenderers.TryGetValue(currentFace.trackableId, out LineRenderer[] lasers))
            {
                Debug.LogWarning("[ARFaceAccessoryController] No se encontraron LineRenderers de láser para el rostro actual.");
                return;
            }

            if (lasers == null || lasers.Length < 2)
            {
                Debug.LogWarning("[ARFaceAccessoryController] Los LineRenderers de láser son insuficientes.");
                return;
            }

            // Establecer temporizador del láser
            laserTimer = laserDuration;

            // Activar y posicionar ambos láseres
            Vector3[] eyeLocalPositions = { LeftEyeLocalPosition, RightEyeLocalPosition };

            for (int i = 0; i < 2; i++)
            {
                LineRenderer lr = lasers[i];
                if (lr == null)
                {
                    continue;
                }

                lr.enabled = true;

                // Calcular posiciones en espacio mundial
                Vector3 eyeWorldPos = currentFace.transform.TransformPoint(eyeLocalPositions[i]);
                Vector3 laserEnd = eyeWorldPos + currentFace.transform.forward * laserLength;

                lr.SetPosition(0, eyeWorldPos);
                lr.SetPosition(1, laserEnd);

                // Actualizar color
                lr.startColor = laserColor;
                lr.endColor = laserColor;
                if (lr.material != null)
                {
                    lr.material.color = laserColor;
                }
            }
        }

        #endregion

        #region Métodos Privados de Láser

        /// <summary>
        /// Actualiza las posiciones de los rayos láser activos para seguir
        /// el movimiento del rostro rastreado.
        /// </summary>
        private void UpdateActiveLaserPositions()
        {
            if (currentFace == null)
            {
                DisableAllLasers();
                return;
            }

            if (!faceLaserRenderers.TryGetValue(currentFace.trackableId, out LineRenderer[] lasers))
            {
                return;
            }

            if (lasers == null)
            {
                return;
            }

            Vector3[] eyeLocalPositions = { LeftEyeLocalPosition, RightEyeLocalPosition };

            for (int i = 0; i < lasers.Length && i < 2; i++)
            {
                LineRenderer lr = lasers[i];
                if (lr == null || !lr.enabled)
                {
                    continue;
                }

                // Recalcular posiciones en espacio mundial según la posición actual del rostro
                Vector3 eyeWorldPos = currentFace.transform.TransformPoint(eyeLocalPositions[i]);
                Vector3 laserEnd = eyeWorldPos + currentFace.transform.forward * laserLength;

                lr.SetPosition(0, eyeWorldPos);
                lr.SetPosition(1, laserEnd);
            }
        }

        /// <summary>
        /// Desactiva todos los LineRenderers de láser en todos los rostros rastreados.
        /// </summary>
        private void DisableAllLasers()
        {
            foreach (KeyValuePair<TrackableId, LineRenderer[]> kvp in faceLaserRenderers)
            {
                LineRenderer[] lasers = kvp.Value;
                if (lasers == null)
                {
                    continue;
                }

                for (int i = 0; i < lasers.Length; i++)
                {
                    if (lasers[i] != null)
                    {
                        lasers[i].enabled = false;
                    }
                }
            }
        }

        #endregion

        #region Utilidades

        /// <summary>
        /// Busca un ARFace rastreado actualmente por su TrackableId.
        /// Itera sobre los trackables del faceManager para encontrar coincidencia.
        /// </summary>
        /// <param name="faceId">ID rastreable del rostro a buscar.</param>
        /// <returns>El ARFace encontrado, o null si no existe o no está rastreado.</returns>
        private ARFace FindTrackedFaceById(TrackableId faceId)
        {
            if (faceManager == null)
            {
                return null;
            }

            foreach (ARFace face in faceManager.trackables)
            {
                if (face != null && face.trackableId == faceId)
                {
                    return face;
                }
            }

            return null;
        }

        #endregion
    }
}
