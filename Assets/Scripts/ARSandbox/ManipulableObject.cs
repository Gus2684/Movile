// ============================================================
// ManipulableObject.cs
// Componente adjunto a cada objeto 3D generado en la escena AR.
// Gestiona visuales de selección, estado de manipulación, color,
// escala, rotación y el modo cinemático del Rigidbody.
// ============================================================

using System;
using UnityEngine;

namespace ARSandbox
{
    /// <summary>
    /// Componente que convierte un GameObject en un objeto manipulable
    /// dentro del sandbox AR. Proporciona indicador visual de selección,
    /// control de escala, rotación, color y estado físico.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ManipulableObject : MonoBehaviour
    {
        // ── Eventos estáticos ───────────────────────────────────────────

        /// <summary>
        /// Evento disparado cuando un ManipulableObject es destruido.
        /// </summary>
        public static event Action<ManipulableObject> OnDestroyed;

        // ── Propiedades públicas ────────────────────────────────────────

        /// <summary>
        /// Referencia al Rigidbody del objeto.
        /// </summary>
        public Rigidbody Rb { get; private set; }

        /// <summary>
        /// Referencia al Renderer principal del objeto.
        /// </summary>
        public Renderer MainRenderer { get; private set; }

        /// <summary>
        /// Indica si el objeto está actualmente seleccionado.
        /// </summary>
        public bool IsSelected { get; private set; }

        /// <summary>
        /// Factor de escala actual aplicado al objeto (rango [0.2, 3.0]).
        /// </summary>
        public float CurrentScale { get; private set; } = 1f;

        /// <summary>
        /// Rotación actual en el eje Y en grados.
        /// </summary>
        public float CurrentRotationY { get; private set; } = 0f;

        // ── Campos privados ─────────────────────────────────────────────

        /// <summary>
        /// Escala original del objeto al momento de su creación.
        /// </summary>
        private Vector3 originalScale;

        /// <summary>
        /// Color original del material del objeto.
        /// </summary>
        private Color originalColor;

        /// <summary>
        /// GameObject del anillo indicador de selección.
        /// </summary>
        private GameObject selectionRing;

        /// <summary>
        /// Renderer del anillo de selección.
        /// </summary>
        private Renderer selectionRingRenderer;

        /// <summary>
        /// Escala mínima permitida.
        /// </summary>
        private const float MinScale = 0.2f;

        /// <summary>
        /// Escala máxima permitida.
        /// </summary>
        private const float MaxScale = 3.0f;

        /// <summary>
        /// Paleta de 9 colores distintos para asignación aleatoria.
        /// </summary>
        private static readonly Color[] colorPalette = new Color[]
        {
            new Color(0.90f, 0.25f, 0.20f, 1f), // Rojo
            new Color(0.20f, 0.65f, 0.32f, 1f), // Verde
            new Color(0.22f, 0.46f, 0.88f, 1f), // Azul
            new Color(0.95f, 0.76f, 0.05f, 1f), // Amarillo
            new Color(0.93f, 0.50f, 0.13f, 1f), // Naranja
            new Color(0.61f, 0.15f, 0.69f, 1f), // Púrpura
            new Color(0.00f, 0.74f, 0.83f, 1f), // Cian
            new Color(0.91f, 0.38f, 0.65f, 1f), // Rosa
            new Color(0.55f, 0.43f, 0.30f, 1f)  // Marrón
        };

        // ═════════════════════════════════════════════════════════════════
        // Ciclo de vida de Unity
        // ═════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Obtener el Rigidbody (garantizado por RequireComponent).
            Rb = GetComponent<Rigidbody>();
            if (Rb != null)
            {
                Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // Obtener el Renderer principal.
            MainRenderer = GetComponent<Renderer>();
            if (MainRenderer == null)
            {
                MainRenderer = GetComponentInChildren<Renderer>();
            }

            if (MainRenderer == null)
            {
                Debug.LogWarning($"[ManipulableObject] No se encontró Renderer en '{gameObject.name}'. Las funciones de color no estarán disponibles.");
            }

            // Guardar la escala original.
            originalScale = transform.localScale;

            // Guardar el color original del material.
            if (MainRenderer != null && MainRenderer.material != null)
            {
                originalColor = MainRenderer.material.color;
            }
            else
            {
                originalColor = Color.white;
            }

            // Crear el indicador de selección (anillo en la base).
            CreateSelectionRing();
        }

        private void OnDestroy()
        {
            // Disparar el evento de destrucción para que otros sistemas
            // puedan reaccionar (p.ej. limpiar listas de seguimiento).
            OnDestroyed?.Invoke(this);
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos públicos
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Activa o desactiva la visualización del anillo de selección.
        /// </summary>
        /// <param name="selected">True para seleccionar, false para deseleccionar.</param>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectionRing != null)
            {
                selectionRing.SetActive(selected);
            }
        }

        /// <summary>
        /// Activa o desactiva el modo cinemático del Rigidbody.
        /// Al activar cinemático, se resetean las velocidades.
        /// </summary>
        /// <param name="kinematic">True para cinemático, false para dinámico.</param>
        public void SetKinematic(bool kinematic)
        {
            if (Rb == null)
            {
                Debug.LogWarning($"[ManipulableObject] Rigidbody nulo en '{gameObject.name}'. No se puede cambiar el estado cinemático.");
                return;
            }

            if (kinematic)
            {
                // Resetear velocidades antes de activar cinemático.
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }

            Rb.isKinematic = kinematic;
        }

        /// <summary>
        /// Establece el color del material principal del objeto.
        /// </summary>
        /// <param name="color">Color a aplicar.</param>
        public void SetColor(Color color)
        {
            if (MainRenderer == null)
            {
                Debug.LogWarning($"[ManipulableObject] Renderer nulo en '{gameObject.name}'. No se puede cambiar el color.");
                return;
            }

            if (MainRenderer.material != null)
            {
                MainRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Asigna un color aleatorio de la paleta predefinida de 9 colores.
        /// </summary>
        public void SetRandomColor()
        {
            int randomIndex = UnityEngine.Random.Range(0, colorPalette.Length);
            SetColor(colorPalette[randomIndex]);
        }

        /// <summary>
        /// Establece la escala del objeto, limitada al rango [0.2, 3.0].
        /// La escala se aplica sobre la escala original del objeto.
        /// </summary>
        /// <param name="scale">Factor de escala deseado.</param>
        public void SetScale(float scale)
        {
            CurrentScale = Mathf.Clamp(scale, MinScale, MaxScale);
            transform.localScale = originalScale * CurrentScale;
        }

        /// <summary>
        /// Establece la rotación del objeto en el eje Y.
        /// </summary>
        /// <param name="degrees">Ángulo de rotación en grados.</param>
        public void SetRotationY(float degrees)
        {
            CurrentRotationY = degrees;
            Vector3 currentEuler = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(currentEuler.x, CurrentRotationY, currentEuler.z);
        }

        /// <summary>
        /// Restaura el color original del objeto (el que tenía al ser creado).
        /// </summary>
        public void ResetColor()
        {
            SetColor(originalColor);
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Crea programáticamente el anillo indicador de selección como
        /// un cilindro aplanado en la base del objeto.
        /// </summary>
        private void CreateSelectionRing()
        {
            // Crear un cilindro primitivo como hijo del objeto.
            selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selectionRing.name = "SelectionRing";
            selectionRing.transform.SetParent(transform, false);

            // Posicionar ligeramente debajo de la base del objeto.
            selectionRing.transform.localPosition = new Vector3(0f, -0.01f, 0f);

            // Aplanar el cilindro para que parezca un anillo/disco.
            selectionRing.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);

            // Destruir el collider del anillo para que no interfiera
            // con las interacciones físicas del objeto.
            Collider ringCollider = selectionRing.GetComponent<Collider>();
            if (ringCollider != null)
            {
                Destroy(ringCollider);
            }

            // Configurar el material semi-transparente color cian.
            selectionRingRenderer = selectionRing.GetComponent<Renderer>();
            if (selectionRingRenderer != null)
            {
                Material ringMaterial = CreateTransparentMaterial();
                if (ringMaterial != null)
                {
                    // Color cian semi-transparente.
                    Color cyanTransparent = new Color(0f, 1f, 1f, 0.4f);
                    ringMaterial.color = cyanTransparent;
                    selectionRingRenderer.material = ringMaterial;
                }
            }

            // Ocultar el anillo por defecto (sólo visible al seleccionar).
            selectionRing.SetActive(false);
        }

        /// <summary>
        /// Crea un material transparente compatible con URP o Built-in.
        /// Intenta primero el shader de URP, y si no está disponible,
        /// utiliza el shader Standard como respaldo.
        /// </summary>
        /// <returns>Material configurado para transparencia.</returns>
        private Material CreateTransparentMaterial()
        {
            Material material = null;

            // Intentar usar el shader de URP primero.
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                material = new Material(urpShader);

                // Configurar transparencia para URP.
                // Establecer el tipo de superficie a Transparente (1).
                material.SetFloat("_Surface", 1f);
                // Modo de blend: Alpha.
                material.SetFloat("_Blend", 0f);
                // Habilitar las palabras clave de transparencia de URP.
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                // Configurar la cola de renderizado.
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                // Configurar el blending.
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_AlphaClip", 0f);

                return material;
            }

            // Respaldo: usar el shader Standard (Built-in Render Pipeline).
            Shader standardShader = Shader.Find("Standard");
            if (standardShader != null)
            {
                material = new Material(standardShader);

                // Configurar el modo de renderizado a Transparente.
                material.SetFloat("_Mode", 3f); // 3 = Transparent
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                return material;
            }

            Debug.LogWarning("[ManipulableObject] No se encontró ningún shader compatible (URP Lit ni Standard). El anillo de selección podría no renderizarse correctamente.");
            return material;
        }
    }
}
