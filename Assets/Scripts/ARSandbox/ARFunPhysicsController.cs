using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARSandbox
{
    /// <summary>
    /// Controlador de mecánicas de física divertidas y caóticas.
    /// Proporciona 4 acciones principales: disparar proyectiles, onda expansiva,
    /// gravedad cero y cámara lenta (bullet time) que afectan a todos los
    /// ManipulableObject generados.
    /// </summary>
    public class ARFunPhysicsController : MonoBehaviour
    {
        #region Campos Serializados

        [Header("Referencias")]
        [SerializeField]
        private ARPlacementAndManipulator placementManager;

        [Header("Proyectiles")]
        [SerializeField, Tooltip("Prefab del proyectil. Si es nulo, se crea una esfera primitiva en tiempo de ejecución.")]
        private GameObject projectilePrefab;

        [SerializeField, Tooltip("Fuerza de disparo del proyectil.")]
        private float projectileForce = 15f;

        [SerializeField, Tooltip("Tiempo de vida del proyectil en segundos.")]
        private float projectileLifetime = 5f;

        [SerializeField, Tooltip("Cantidad máxima de proyectiles activos simultáneamente.")]
        private int maxProjectiles = 10;

        [Header("Onda Expansiva")]
        [SerializeField, Tooltip("Fuerza de la onda expansiva.")]
        private float shockwaveForce = 500f;

        [SerializeField, Tooltip("Radio de alcance de la onda expansiva.")]
        private float shockwaveRadius = 5f;

        [Header("Gravedad Cero")]
        [SerializeField, Tooltip("Amplitud del movimiento sinusoidal de flotación en gravedad cero.")]
        private float zeroGFloatAmplitude = 0.5f;

        [SerializeField, Tooltip("Frecuencia del movimiento sinusoidal de flotación en gravedad cero.")]
        private float zeroGFloatFrequency = 1f;

        #endregion

        #region Eventos Estáticos

        /// <summary>
        /// Evento disparado cuando se activa o desactiva la gravedad cero.
        /// El parámetro booleano indica si está activa (true) o inactiva (false).
        /// </summary>
        public static event Action<bool> OnZeroGToggled;

        /// <summary>
        /// Evento disparado cuando se activa o desactiva el modo cámara lenta.
        /// El parámetro booleano indica si está activo (true) o inactivo (false).
        /// </summary>
        public static event Action<bool> OnBulletTimeToggled;

        #endregion

        #region Propiedades Públicas

        /// <summary>
        /// Indica si la gravedad cero está actualmente activa.
        /// </summary>
        public bool IsZeroGActive { get; private set; }

        /// <summary>
        /// Indica si el modo cámara lenta está actualmente activo.
        /// </summary>
        public bool IsBulletTimeActive { get; private set; }

        #endregion

        #region Campos Privados

        /// <summary>
        /// Cola de proyectiles activos para reciclaje (pool simple).
        /// </summary>
        private Queue<GameObject> projectilePool;

        /// <summary>
        /// Gravedad original del sistema de física, guardada para restaurar.
        /// </summary>
        private Vector3 originalGravity;

        #endregion

        #region Ciclo de Vida de Unity

        /// <summary>
        /// Inicialización temprana: busca referencias faltantes y guarda la gravedad original.
        /// </summary>
        private void Awake()
        {
            projectilePool = new Queue<GameObject>();

            // Guardar la gravedad original del sistema de física
            originalGravity = Physics.gravity;

            // Buscar el gestor de colocación si no fue asignado en el inspector
            if (placementManager == null)
            {
                placementManager = FindObjectOfType<ARPlacementAndManipulator>();
                if (placementManager == null)
                {
                    Debug.LogWarning("[ARFunPhysicsController] No se encontró ARPlacementAndManipulator en la escena.");
                }
            }
        }

        /// <summary>
        /// Actualización de física: aplica fuerzas sinusoidales de flotación
        /// cuando la gravedad cero está activa.
        /// </summary>
        private void FixedUpdate()
        {
            if (!IsZeroGActive)
            {
                return;
            }

            if (placementManager == null)
            {
                return;
            }

            List<ManipulableObject> spawnedObjects = placementManager.SpawnedObjects;
            if (spawnedObjects == null)
            {
                return;
            }

            // Aplicar fuerza sinusoidal y torque aleatorio a cada objeto generado
            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                ManipulableObject obj = spawnedObjects[i];
                if (obj == null)
                {
                    continue;
                }

                Rigidbody rb = obj.Rb;
                if (rb == null || rb.isKinematic)
                {
                    continue;
                }

                // Fuerza sinusoidal única por objeto usando su hash como desfase
                float sinForce = Mathf.Sin(Time.time * zeroGFloatFrequency + obj.GetHashCode()) * zeroGFloatAmplitude;
                rb.AddForce(Vector3.up * sinForce, ForceMode.Acceleration);

                // Torque aleatorio suave para rotación orgánica
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.1f, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Al desactivar el componente, restaurar la gravedad y la escala de tiempo
        /// a sus valores predeterminados para evitar estados corruptos.
        /// </summary>
        private void OnDisable()
        {
            // Restaurar gravedad original
            Physics.gravity = originalGravity;
            IsZeroGActive = false;

            // Restaurar escala de tiempo
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            IsBulletTimeActive = false;
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Dispara un proyectil desde la posición de la cámara principal hacia adelante.
        /// Gestiona un pool simple: si se excede el máximo, destruye el más antiguo.
        /// Si no hay prefab asignado, crea una esfera primitiva con TrailRenderer.
        /// </summary>
        public void ShootProjectile()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[ARFunPhysicsController] No se encontró la cámara principal para disparar el proyectil.");
                return;
            }

            // Posición y dirección de disparo
            Vector3 spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * 0.2f;
            Vector3 shootDirection = mainCamera.transform.forward;

            GameObject projectile = null;

            if (projectilePrefab != null)
            {
                // Instanciar desde prefab
                projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                // Crear esfera primitiva programáticamente
                projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = "Proyectil_Esfera";
                projectile.transform.position = spawnPosition;
                projectile.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

                // Agregar Rigidbody si no existe
                Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
                if (projectileRb == null)
                {
                    projectileRb = projectile.AddComponent<Rigidbody>();
                }
                projectileRb.useGravity = true;
                projectileRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Agregar TrailRenderer para efecto visual
                TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
                trail.time = 0.5f;
                trail.startWidth = 0.05f;
                trail.endWidth = 0f;
                trail.startColor = Color.Lerp(Color.red, Color.yellow, 0.5f); // Naranja
                trail.endColor = Color.yellow;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.minVertexDistance = 0.01f;
            }

            if (projectile == null)
            {
                Debug.LogError("[ARFunPhysicsController] No se pudo crear el proyectil.");
                return;
            }

            // Aplicar velocidad al proyectil
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = projectile.AddComponent<Rigidbody>();
            }
            rb.linearVelocity = shootDirection * projectileForce;

            // Retroalimentación háptica en dispositivos móviles al disparar
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            Handheld.Vibrate();
#endif

            // Agregar a la cola del pool
            projectilePool.Enqueue(projectile);

            // Si se excede el máximo de proyectiles, destruir el más antiguo
            while (projectilePool.Count > maxProjectiles)
            {
                GameObject oldest = projectilePool.Dequeue();
                if (oldest != null)
                {
                    Destroy(oldest);
                }
            }

            // Auto-destruir después del tiempo de vida
            Destroy(projectile, projectileLifetime);
        }

        /// <summary>
        /// Desencadena una onda expansiva desde el centro de masa de todos los objetos generados.
        /// Aplica fuerza de explosión radial a cada objeto con Rigidbody.
        /// </summary>
        public void TriggerShockwave()
        {
            if (placementManager == null)
            {
                Debug.LogWarning("[ARFunPhysicsController] No hay placementManager asignado para la onda expansiva.");
                return;
            }

            List<ManipulableObject> spawnedObjects = placementManager.SpawnedObjects;
            if (spawnedObjects == null || spawnedObjects.Count == 0)
            {
                Debug.LogWarning("[ARFunPhysicsController] No hay objetos generados para aplicar la onda expansiva.");
                return;
            }

            // Calcular el centro de masa de todos los objetos
            Vector3 centerOfMass = Vector3.zero;
            int validCount = 0;

            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                ManipulableObject obj = spawnedObjects[i];
                if (obj == null)
                {
                    continue;
                }

                centerOfMass += obj.transform.position;
                validCount++;
            }

            if (validCount == 0)
            {
                Debug.LogWarning("[ARFunPhysicsController] No se encontraron objetos válidos para calcular el centro de masa.");
                return;
            }

            centerOfMass /= validCount;

            // Aplicar fuerza de explosión a cada objeto
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

                // Desactivar cinemático para que reciba fuerzas
                obj.SetKinematic(false);

                // Aplicar fuerza de explosión con componente vertical
                rb.AddExplosionForce(shockwaveForce, centerOfMass, shockwaveRadius, 0.5f, ForceMode.Impulse);
            }

            // Retroalimentación háptica en móvil al detonar la onda expansiva
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            Handheld.Vibrate();
#endif
        }

        /// <summary>
        /// Alterna el estado de gravedad cero. Cuando se activa, establece la gravedad
        /// del sistema a cero y desactiva el estado cinemático de todos los objetos.
        /// Cuando se desactiva, restaura la gravedad original.
        /// </summary>
        public void ToggleZeroG()
        {
            IsZeroGActive = !IsZeroGActive;

            if (IsZeroGActive)
            {
                // Activar gravedad cero
                Physics.gravity = Vector3.zero;

                // Desactivar cinemático en todos los objetos para que floten
                if (placementManager != null)
                {
                    List<ManipulableObject> spawnedObjects = placementManager.SpawnedObjects;
                    if (spawnedObjects != null)
                    {
                        for (int i = 0; i < spawnedObjects.Count; i++)
                        {
                            ManipulableObject obj = spawnedObjects[i];
                            if (obj != null && obj.Rb != null)
                            {
                                obj.SetKinematic(false);
                            }
                        }
                    }
                }
            }
            else
            {
                // Restaurar gravedad original
                Physics.gravity = originalGravity;
            }

            // Disparar evento
            OnZeroGToggled?.Invoke(IsZeroGActive);
        }

        /// <summary>
        /// Alterna el modo cámara lenta (bullet time). Cuando se activa, reduce
        /// la escala de tiempo a 0.25x. Cuando se desactiva, restaura la escala normal.
        /// </summary>
        public void ToggleBulletTime()
        {
            IsBulletTimeActive = !IsBulletTimeActive;

            if (IsBulletTimeActive)
            {
                // Activar cámara lenta
                Time.timeScale = 0.25f;
                Time.fixedDeltaTime = 0.02f * 0.25f;
            }
            else
            {
                // Restaurar velocidad normal
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }

            // Disparar evento
            OnBulletTimeToggled?.Invoke(IsBulletTimeActive);
        }

        #endregion
    }
}
