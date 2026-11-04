// ============================================================
// ARModeManager.cs
// Gestor central de modos AR. Alterna entre Sandbox, ImageMarker
// y FaceFilter sin recargar la escena, habilitando/deshabilitando
// los managers y controladores correspondientes de AR Foundation.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARSandbox
{
    /// <summary>
    /// Enumeración que define los tres modos AR disponibles.
    /// </summary>
    public enum ARMode
    {
        Sandbox,
        ImageMarker,
        FaceFilter
    }

    /// <summary>
    /// Controlador central que gestiona las transiciones entre los distintos
    /// modos de realidad aumentada, activando y desactivando los managers
    /// y grupos de controladores necesarios para cada modo.
    /// </summary>
    public class ARModeManager : MonoBehaviour
    {
        // ── Referencias a managers de AR Foundation ──────────────────────

        [Header("Managers de AR Foundation")]
        [SerializeField] private ARPlaneManager arPlaneManager;
        [SerializeField] private ARRaycastManager arRaycastManager;
        [SerializeField] private ARTrackedImageManager arTrackedImageManager;
        [SerializeField] private ARFaceManager arFaceManager;
        [SerializeField] private ARCameraManager arCameraManager;

        // ── Grupos de controladores por modo ────────────────────────────

        [Header("Grupos de controladores por modo")]
        [SerializeField] private GameObject sandboxControllers;
        [SerializeField] private GameObject imageControllers;
        [SerializeField] private GameObject faceControllers;

        // ── Eventos ─────────────────────────────────────────────────────

        /// <summary>
        /// Evento estático disparado cada vez que el modo AR cambia.
        /// </summary>
        public static event Action<ARMode> OnModeChanged;

        // ── Propiedades públicas ────────────────────────────────────────

        /// <summary>
        /// Modo AR actualmente activo.
        /// </summary>
        public ARMode CurrentMode { get; private set; } = ARMode.Sandbox;

        // ═════════════════════════════════════════════════════════════════
        // Ciclo de vida de Unity
        // ═════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // Intentar encontrar los managers automáticamente si no fueron
            // asignados desde el Inspector.
            if (arPlaneManager == null)
            {
                arPlaneManager = FindObjectOfType<ARPlaneManager>();
                if (arPlaneManager != null)
                {
                    Debug.Log("[ARModeManager] ARPlaneManager encontrado automáticamente.");
                }
            }

            if (arRaycastManager == null)
            {
                arRaycastManager = FindObjectOfType<ARRaycastManager>();
                if (arRaycastManager != null)
                {
                    Debug.Log("[ARModeManager] ARRaycastManager encontrado automáticamente.");
                }
            }

            if (arTrackedImageManager == null)
            {
                arTrackedImageManager = FindObjectOfType<ARTrackedImageManager>();
                if (arTrackedImageManager != null)
                {
                    Debug.Log("[ARModeManager] ARTrackedImageManager encontrado automáticamente.");
                }
            }

            if (arFaceManager == null)
            {
                arFaceManager = FindObjectOfType<ARFaceManager>();
                if (arFaceManager != null)
                {
                    Debug.Log("[ARModeManager] ARFaceManager encontrado automáticamente.");
                }
            }

            if (arCameraManager == null)
            {
                arCameraManager = FindObjectOfType<ARCameraManager>();
                if (arCameraManager != null)
                {
                    Debug.Log("[ARModeManager] ARCameraManager encontrado automáticamente.");
                }
            }
        }

        private void Start()
        {
            // Iniciar siempre en modo Sandbox.
            SetMode(ARMode.Sandbox);
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos públicos
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Establece el modo AR activo. Desactiva todos los managers y
        /// controladores primero, luego activa sólo los correspondientes
        /// al modo seleccionado.
        /// </summary>
        /// <param name="mode">Modo AR al que se desea cambiar.</param>
        public void SetMode(ARMode mode)
        {
            // Paso 1: Desactivar todos los managers y controladores.
            DisableAllManagersAndControllers();

            // Paso 2: Activar los componentes correspondientes al modo.
            switch (mode)
            {
                case ARMode.Sandbox:
                    EnableSandboxMode();
                    break;

                case ARMode.ImageMarker:
                    EnableImageMarkerMode();
                    break;

                case ARMode.FaceFilter:
                    EnableFaceFilterMode();
                    break;
            }

            // Actualizar el modo actual y disparar el evento.
            CurrentMode = mode;
            OnModeChanged?.Invoke(CurrentMode);

            Debug.Log($"[ARModeManager] Modo cambiado a: {CurrentMode}");
        }

        /// <summary>
        /// Método de conveniencia para cambiar al modo Sandbox.
        /// </summary>
        public void SetModeSandbox()
        {
            SetMode(ARMode.Sandbox);
        }

        /// <summary>
        /// Método de conveniencia para cambiar al modo ImageMarker.
        /// </summary>
        public void SetModeImageMarker()
        {
            SetMode(ARMode.ImageMarker);
        }

        /// <summary>
        /// Método de conveniencia para cambiar al modo FaceFilter.
        /// </summary>
        public void SetModeFaceFilter()
        {
            SetMode(ARMode.FaceFilter);
        }

        // ═════════════════════════════════════════════════════════════════
        // Métodos privados
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Desactiva todos los managers de AR Foundation y todos los
        /// grupos de controladores.
        /// </summary>
        private void DisableAllManagersAndControllers()
        {
            // Desactivar managers de AR Foundation.
            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = false;
            }

            if (arRaycastManager != null)
            {
                arRaycastManager.enabled = false;
            }

            if (arTrackedImageManager != null)
            {
                arTrackedImageManager.enabled = false;
            }

            if (arFaceManager != null)
            {
                arFaceManager.enabled = false;
            }

            // Desactivar todos los grupos de controladores.
            if (sandboxControllers != null)
            {
                sandboxControllers.SetActive(false);
            }

            if (imageControllers != null)
            {
                imageControllers.SetActive(false);
            }

            if (faceControllers != null)
            {
                faceControllers.SetActive(false);
            }
        }

        /// <summary>
        /// Activa los componentes necesarios para el modo Sandbox:
        /// ARPlaneManager, ARRaycastManager, controladores de sandbox
        /// y cámara trasera (World).
        /// </summary>
        private void EnableSandboxMode()
        {
            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = true;
            }
            else
            {
                Debug.LogWarning("[ARModeManager] ARPlaneManager no asignado. El modo Sandbox podría no funcionar correctamente.");
            }

            if (arRaycastManager != null)
            {
                arRaycastManager.enabled = true;
            }
            else
            {
                Debug.LogWarning("[ARModeManager] ARRaycastManager no asignado. El modo Sandbox podría no funcionar correctamente.");
            }

            if (sandboxControllers != null)
            {
                sandboxControllers.SetActive(true);
            }

            // Configurar la cámara para usar la cámara trasera (mundo).
            SetCameraFacingDirection(CameraFacingDirection.World);
        }

        /// <summary>
        /// Activa los componentes necesarios para el modo ImageMarker:
        /// ARTrackedImageManager, controladores de imagen y cámara
        /// trasera (World).
        /// </summary>
        private void EnableImageMarkerMode()
        {
            if (arTrackedImageManager != null)
            {
                arTrackedImageManager.enabled = true;
            }
            else
            {
                Debug.LogWarning("[ARModeManager] ARTrackedImageManager no asignado. El modo ImageMarker podría no funcionar correctamente.");
            }

            if (imageControllers != null)
            {
                imageControllers.SetActive(true);
            }

            // Configurar la cámara para usar la cámara trasera (mundo).
            SetCameraFacingDirection(CameraFacingDirection.World);
        }

        /// <summary>
        /// Activa los componentes necesarios para el modo FaceFilter:
        /// ARFaceManager, controladores de filtros faciales y cámara
        /// frontal (User).
        /// </summary>
        private void EnableFaceFilterMode()
        {
            if (arFaceManager != null)
            {
                arFaceManager.enabled = true;
            }
            else
            {
                Debug.LogWarning("[ARModeManager] ARFaceManager no asignado. El modo FaceFilter podría no funcionar correctamente.");
            }

            if (faceControllers != null)
            {
                faceControllers.SetActive(true);
            }

            // Configurar la cámara para usar la cámara frontal (usuario).
            SetCameraFacingDirection(CameraFacingDirection.User);
        }

        /// <summary>
        /// Configura la dirección de la cámara AR (frontal o trasera).
        /// </summary>
        /// <param name="direction">Dirección deseada de la cámara.</param>
        private void SetCameraFacingDirection(CameraFacingDirection direction)
        {
            if (arCameraManager != null)
            {
                arCameraManager.requestedFacingDirection = direction;
            }
            else
            {
                Debug.LogWarning("[ARModeManager] ARCameraManager no asignado. No se puede cambiar la dirección de la cámara.");
            }
        }
    }
}
