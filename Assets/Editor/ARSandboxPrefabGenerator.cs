// =============================================================================
// ARSandboxPrefabGenerator.cs
// Script de Editor que genera automáticamente todos los prefabs de demostración
// y permite construir la escena completa de prueba con 1 solo clic.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using ARSandbox;

/// <summary>
/// Generador de prefabs y configurador integral de escena para el AR Physics Sandbox.
/// Accesible desde el menú superior:
///  1. Tools > AR Sandbox > 1-Click Complete Scene Setup (Recomendado)
///  2. Tools > AR Sandbox > Generate Demo Prefabs (Solo prefabs)
/// </summary>
public class ARSandboxPrefabGenerator
{
    private const string PrefabFolder = "Assets/Prefabs/ARSandbox";
    private const string MaterialsFolder = "Assets/Prefabs/ARSandbox/Materials";

    // =========================================================================
    // MENÚ PRINCIPAL: CONFIGURACIÓN INTEGRAL DE ESCENA CON 1 CLIC
    // =========================================================================

    [MenuItem("Tools/AR Sandbox/1-Click Complete Scene Setup", false, 0)]
    public static void SetupCompletePlayableScene()
    {
        // 1. Generar o actualizar prefabs y materiales guardados en disco
        GenerateDemoPrefabsInternal(false);

        // 2. Asegurar que haya una escena válida
        UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            EnsureFolderExists("Assets/Scenes");
            activeScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(activeScene, "Assets/Scenes/ARSandboxScene.unity");
        }

        // 3. Crear o limpiar elementos existentes de AR Sandbox
        GameObject existingManagers = GameObject.Find("Managers");
        if (existingManagers != null) Object.DestroyImmediate(existingManagers);

        GameObject existingCanvas = GameObject.Find("Canvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        GameObject existingFloor = GameObject.Find("SimulatedFloor_EditorTesting");
        if (existingFloor != null) Object.DestroyImmediate(existingFloor);

        GameObject existingContainer = GameObject.Find("SpawnedObjectsContainer");
        if (existingContainer != null) Object.DestroyImmediate(existingContainer);

        // 4. Configurar iluminación
        Light dirLight = Object.FindAnyObjectByType<Light>();
        if (dirLight == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        dirLight.intensity = 1.2f;

        // 5. Configurar o crear XR Origin
        GameObject xrOriginObj = GameObject.Find("XR Origin");
        if (xrOriginObj == null)
        {
            xrOriginObj = GameObject.Find("XR Rig");
        }
        if (xrOriginObj == null)
        {
            xrOriginObj = new GameObject("XR Origin");
            xrOriginObj.transform.position = Vector3.zero;
        }

        // Cámara principal
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        // Posicionar la cámara para ver el suelo tanto en editor como en móvil
        if (mainCam.transform.parent == null)
        {
            GameObject camOffset = new GameObject("Camera Offset");
            camOffset.transform.SetParent(xrOriginObj.transform, false);
            mainCam.transform.SetParent(camOffset.transform, false);
        }
        mainCam.transform.localPosition = new Vector3(0f, 1.2f, -1.5f);
        mainCam.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

        // Componentes AR en la Cámara
        EnsureComponent<ARCameraManager>(mainCam.gameObject);
        EnsureComponent<ARCameraBackground>(mainCam.gameObject);

        // Componentes AR en XR Origin
        ARPlaneManager planeManager = EnsureComponent<ARPlaneManager>(xrOriginObj);
        ARRaycastManager raycastManager = EnsureComponent<ARRaycastManager>(xrOriginObj);
        ARTrackedImageManager imageManager = EnsureComponent<ARTrackedImageManager>(xrOriginObj);
        ARFaceManager faceManager = EnsureComponent<ARFaceManager>(xrOriginObj);

        // Asignar el prefab del plano AR con BoxCollider
        GameObject defaultPlanePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/ARDefaultPlane.prefab");
        if (defaultPlanePrefab != null && planeManager != null)
        {
            SerializedObject planeSer = new SerializedObject(planeManager);
            SerializedProperty planeProp = planeSer.FindProperty("m_PlanePrefab");
            if (planeProp != null)
            {
                planeProp.objectReferenceValue = defaultPlanePrefab;
                planeSer.ApplyModifiedProperties();
            }
        }

        // 6. Configurar AR Session
        GameObject arSessionObj = GameObject.Find("AR Session");
        if (arSessionObj == null)
        {
            arSessionObj = new GameObject("AR Session");
        }
        EnsureComponent<ARSession>(arSessionObj);
        EnsureComponent<ARInputManager>(arSessionObj);

        // 7. Contenedor de objetos spawneados
        GameObject spawnedContainer = new GameObject("SpawnedObjectsContainer");
        spawnedContainer.transform.position = Vector3.zero;

        // 8. Crear Suelo Simulado para pruebas instantáneas en Play Mode en PC
        GameObject simFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        simFloor.name = "SimulatedFloor_EditorTesting";
        simFloor.transform.position = new Vector3(0f, -0.05f, 0f);
        simFloor.transform.localScale = new Vector3(6f, 0.1f, 6f);
        Material floorMat = CreateMaterial("Mat_EditorSimulatedFloor", new Color(0.18f, 0.2f, 0.24f));
        SetRendererMaterial(simFloor, floorMat);

        // 9. Crear GameObject Managers con los 6 controladores
        GameObject managersObj = new GameObject("Managers");
        ARModeManager modeManager = managersObj.AddComponent<ARModeManager>();
        ARPlacementAndManipulator placementManager = managersObj.AddComponent<ARPlacementAndManipulator>();
        ARFunPhysicsController physicsController = managersObj.AddComponent<ARFunPhysicsController>();
        ARImagePortalTracker imageTracker = managersObj.AddComponent<ARImagePortalTracker>();
        ARFaceAccessoryController faceController = managersObj.AddComponent<ARFaceAccessoryController>();
        ARUIManager uiManager = managersObj.AddComponent<ARUIManager>();

        // Asignar prefabs a ARPlacementAndManipulator
        GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Block.prefab");
        GameObject rampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Ramp.prefab");
        GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Target.prefab");
        GameObject reticlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/PlacementReticle.prefab");
        GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Projectile.prefab");
        GameObject portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/PortalPlatform.prefab");
        GameObject glassesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/CyberGlasses.prefab");
        GameObject crownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Crown.prefab");

        SerializedObject placeSer = new SerializedObject(placementManager);
        SerializedProperty prefabsListProp = placeSer.FindProperty("spawnablePrefabs");
        if (prefabsListProp != null)
        {
            prefabsListProp.arraySize = 3;
            if (blockPrefab != null) prefabsListProp.GetArrayElementAtIndex(0).objectReferenceValue = blockPrefab;
            if (rampPrefab != null) prefabsListProp.GetArrayElementAtIndex(1).objectReferenceValue = rampPrefab;
            if (targetPrefab != null) prefabsListProp.GetArrayElementAtIndex(2).objectReferenceValue = targetPrefab;
        }
        SerializedProperty reticleProp = placeSer.FindProperty("reticlePrefab");
        if (reticleProp != null && reticlePrefab != null) reticleProp.objectReferenceValue = reticlePrefab;
        SerializedProperty contProp = placeSer.FindProperty("spawnedObjectsContainer");
        if (contProp != null) contProp.objectReferenceValue = spawnedContainer.transform;
        placeSer.ApplyModifiedProperties();

        // Asignar proyectil a ARFunPhysicsController
        if (projectilePrefab != null)
        {
            SerializedObject physSer = new SerializedObject(physicsController);
            SerializedProperty projProp = physSer.FindProperty("projectilePrefab");
            if (projProp != null) projProp.objectReferenceValue = projectilePrefab;
            physSer.ApplyModifiedProperties();
        }

        // Asignar portal a ARImagePortalTracker
        if (portalPrefab != null)
        {
            SerializedObject portalSer = new SerializedObject(imageTracker);
            SerializedProperty portProp = portalSer.FindProperty("portalPrefab");
            if (portProp != null) portProp.objectReferenceValue = portalPrefab;
            portalSer.ApplyModifiedProperties();
        }

        // Asignar accesorios a ARFaceAccessoryController
        SerializedObject faceSer = new SerializedObject(faceController);
        SerializedProperty accListProp = faceSer.FindProperty("accessoryPrefabs");
        if (accListProp != null)
        {
            accListProp.arraySize = 2;
            if (glassesPrefab != null) accListProp.GetArrayElementAtIndex(0).objectReferenceValue = glassesPrefab;
            if (crownPrefab != null) accListProp.GetArrayElementAtIndex(1).objectReferenceValue = crownPrefab;
        }
        faceSer.ApplyModifiedProperties();

        // 10. Crear Canvas UI Completo y Funcional
        BuildCompleteUI(uiManager, modeManager, placementManager, physicsController, imageTracker, faceController);

        // Guardar y refrescar escena
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = managersObj;

        EditorUtility.DisplayDialog(
            "AR Sandbox — Configuración Completa",
            "¡Escena configurada y lista para jugar!\n\n" +
            "• Prefabs generados con materiales URP listos.\n" +
            "• Canvas, botones, sliders y paneles creados.\n" +
            "• Suelo simulado añadido para pruebas en PC.\n\n" +
            "¡Presiona PLAY en Unity para probar el juego con clics de ratón, o compila en tu móvil para AR real!",
            "¡A Jugar!"
        );
    }

    // =========================================================================
    // GENERADOR DE PREFABS INDIVIDUAL
    // =========================================================================

    [MenuItem("Tools/AR Sandbox/Generate Demo Prefabs", false, 1)]
    public static void GenerateDemoPrefabs()
    {
        GenerateDemoPrefabsInternal(true);
    }

    private static void GenerateDemoPrefabsInternal(bool showDialog)
    {
        EnsureFolderExists(PrefabFolder);
        EnsureFolderExists(MaterialsFolder);
        EnsureTagExists("SpawnedObject");

        int successCount = 0;
        if (CreateBlockPrefab()) successCount++;
        if (CreateRampPrefab()) successCount++;
        if (CreateTargetPrefab()) successCount++;
        if (CreateProjectilePrefab()) successCount++;
        if (CreatePlacementReticlePrefab()) successCount++;
        if (CreatePortalPlatformPrefab()) successCount++;
        if (CreateCyberGlassesPrefab()) successCount++;
        if (CreateCrownPrefab()) successCount++;
        if (CreateARDefaultPlanePrefab()) successCount++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "AR Sandbox Prefabs",
                $"Prefabs generados con éxito: {successCount}/9\nGuardados en: {PrefabFolder}",
                "Aceptar"
            );
        }
    }

    // =========================================================================
    // CONSTRUCCIÓN DEL CANVAS Y UI
    // =========================================================================

    private static void BuildCompleteUI(
        ARUIManager uiManager,
        ARModeManager modeMgr,
        ARPlacementAndManipulator placeMgr,
        ARFunPhysicsController physCtrl,
        ARImagePortalTracker imgTracker,
        ARFaceAccessoryController faceCtrl)
    {
        // 1. Canvas principal
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // EventSystem
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Estilos base
        Color darkPanelColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        Color btnBlue = new Color(0.18f, 0.45f, 0.85f);
        Color btnGreen = new Color(0.18f, 0.75f, 0.4f);
        Color btnOrange = new Color(0.95f, 0.55f, 0.15f);
        Color btnRed = new Color(0.85f, 0.25f, 0.25f);

        // =====================================================================
        // TOP BAR
        // =====================================================================
        GameObject topBar = CreatePanel("TopBar", canvasObj.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 100), darkPanelColor);

        Button btnModeSandbox = CreateButton("BtnModeSandbox", topBar.transform, new Vector2(180, 70), new Vector2(-320, 0), "Sandbox", btnBlue);
        Button btnModeImage = CreateButton("BtnModeImage", topBar.transform, new Vector2(180, 70), new Vector2(-120, 0), "Marcador", btnBlue);
        Button btnModeFace = CreateButton("BtnModeFace", topBar.transform, new Vector2(180, 70), new Vector2(80, 0), "Rostro", btnBlue);

        TextMeshProUGUI txtCount = CreateTMP("TxtObjectCount", topBar.transform, new Vector2(250, 45), new Vector2(340, 20), "Objetos: 0", 22, TextAlignmentOptions.Right);
        TextMeshProUGUI txtStatus = CreateTMP("TxtTrackingStatus", topBar.transform, new Vector2(250, 40), new Vector2(340, -25), "Tracking: Activo", 18, TextAlignmentOptions.Right);

        // =====================================================================
        // CATALOG PANEL (Inferior)
        // =====================================================================
        GameObject catalogPanel = CreatePanel("CatalogPanel", canvasObj.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 110), darkPanelColor);

        Button btnBlock = CreateButton("BtnSpawnBlock", catalogPanel.transform, new Vector2(200, 75), new Vector2(-330, 0), "🟦 Bloque", btnBlue);
        Button btnRamp = CreateButton("BtnSpawnRamp", catalogPanel.transform, new Vector2(200, 75), new Vector2(-110, 0), "🟩 Rampa", btnGreen);
        Button btnTarget = CreateButton("BtnSpawnTarget", catalogPanel.transform, new Vector2(200, 75), new Vector2(110, 0), "🎯 Diana", btnRed);
        Button btnClear = CreateButton("BtnClearScene", catalogPanel.transform, new Vector2(180, 75), new Vector2(330, 0), "🗑️ Limpiar", new Color(0.5f, 0.2f, 0.2f));

        // =====================================================================
        // CHAOS PANEL (Lateral Derecho)
        // =====================================================================
        GameObject chaosPanel = CreatePanel("ChaosPanel", canvasObj.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-75, 0), new Vector2(130, 420), darkPanelColor);

        Button btnShoot = CreateButton("BtnShoot", chaosPanel.transform, new Vector2(110, 80), new Vector2(0, 150), "🚀\nDisparar", btnOrange);
        Button btnShockwave = CreateButton("BtnShockwave", chaosPanel.transform, new Vector2(110, 80), new Vector2(0, 50), "💥\nOnda", btnRed);
        Button btnZeroG = CreateButton("BtnZeroG", chaosPanel.transform, new Vector2(110, 80), new Vector2(0, -50), "🛸\nZero-G", new Color(0.5f, 0.2f, 0.8f));
        Button btnBulletTime = CreateButton("BtnBulletTime", chaosPanel.transform, new Vector2(110, 80), new Vector2(0, -150), "⏱️\nLento", new Color(0.2f, 0.6f, 0.8f));

        // =====================================================================
        // INSPECTOR PANEL (Contextual)
        // =====================================================================
        GameObject inspectorPanel = CreatePanel("InspectorPanel", canvasObj.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(180, 0), new Vector2(320, 420), darkPanelColor);

        CreateTMP("LblRot", inspectorPanel.transform, new Vector2(280, 30), new Vector2(0, 170), "Rotación Y:", 20, TextAlignmentOptions.Left);
        Slider sliderRot = CreateSlider("SliderRotation", inspectorPanel.transform, new Vector2(280, 30), new Vector2(0, 135), 0f, 360f);
        TextMeshProUGUI txtRotVal = CreateTMP("TxtRotVal", inspectorPanel.transform, new Vector2(280, 25), new Vector2(0, 105), "0°", 18, TextAlignmentOptions.Center);

        CreateTMP("LblScale", inspectorPanel.transform, new Vector2(280, 30), new Vector2(0, 65), "Escala:", 20, TextAlignmentOptions.Left);
        Slider sliderScale = CreateSlider("SliderScale", inspectorPanel.transform, new Vector2(280, 30), new Vector2(0, 30), 0.2f, 3f);
        sliderScale.value = 1f;
        TextMeshProUGUI txtScaleVal = CreateTMP("TxtScaleVal", inspectorPanel.transform, new Vector2(280, 25), new Vector2(0, 0), "1.0x", 18, TextAlignmentOptions.Center);

        Button btnColor = CreateButton("BtnColor", inspectorPanel.transform, new Vector2(280, 50), new Vector2(0, -45), "🎨 Cambiar Color", btnGreen);
        Toggle toggleKinematic = CreateToggle("ToggleKinematic", inspectorPanel.transform, new Vector2(280, 45), new Vector2(0, -100), "Fijar en el aire (Kinematic)");
        Button btnDelete = CreateButton("BtnDelete", inspectorPanel.transform, new Vector2(280, 50), new Vector2(0, -155), "🗑️ Eliminar Objeto", btnRed);

        // =====================================================================
        // FACE PANEL (Modo Rostro)
        // =====================================================================
        GameObject facePanel = CreatePanel("FacePanel", canvasObj.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(500, 100), darkPanelColor);
        Button btnCycleFace = CreateButton("BtnCycleFace", facePanel.transform, new Vector2(220, 75), new Vector2(-120, 0), "👑 Cambiar Accesorio", btnOrange);
        Button btnShootLaser = CreateButton("BtnShootLaser", facePanel.transform, new Vector2(220, 75), new Vector2(120, 0), "⚡ Ojos Láser", btnRed);
        facePanel.SetActive(false);

        // =====================================================================
        // VINCULACIÓN COMPLETA A ARUIManager
        // =====================================================================
        SerializedObject uiSer = new SerializedObject(uiManager);

        // Managers
        uiSer.FindProperty("modeManager").objectReferenceValue = modeMgr;
        uiSer.FindProperty("placementManager").objectReferenceValue = placeMgr;
        uiSer.FindProperty("physicsController").objectReferenceValue = physCtrl;
        uiSer.FindProperty("imageTracker").objectReferenceValue = imgTracker;
        uiSer.FindProperty("faceController").objectReferenceValue = faceCtrl;

        // Barra superior
        uiSer.FindProperty("btnModeSandbox").objectReferenceValue = btnModeSandbox;
        uiSer.FindProperty("btnModeImage").objectReferenceValue = btnModeImage;
        uiSer.FindProperty("btnModeFace").objectReferenceValue = btnModeFace;
        uiSer.FindProperty("txtObjectCount").objectReferenceValue = txtCount;
        uiSer.FindProperty("txtTrackingStatus").objectReferenceValue = txtStatus;

        SerializedProperty modeBgProp = uiSer.FindProperty("modeButtonBackgrounds");
        modeBgProp.arraySize = 3;
        modeBgProp.GetArrayElementAtIndex(0).objectReferenceValue = btnModeSandbox.GetComponent<Image>();
        modeBgProp.GetArrayElementAtIndex(1).objectReferenceValue = btnModeImage.GetComponent<Image>();
        modeBgProp.GetArrayElementAtIndex(2).objectReferenceValue = btnModeFace.GetComponent<Image>();

        // Catálogo
        uiSer.FindProperty("catalogPanel").objectReferenceValue = catalogPanel;
        uiSer.FindProperty("btnSpawnBlock").objectReferenceValue = btnBlock;
        uiSer.FindProperty("btnSpawnRamp").objectReferenceValue = btnRamp;
        uiSer.FindProperty("btnSpawnTarget").objectReferenceValue = btnTarget;
        uiSer.FindProperty("btnClearScene").objectReferenceValue = btnClear;

        SerializedProperty catBgProp = uiSer.FindProperty("catalogButtonBackgrounds");
        catBgProp.arraySize = 3;
        catBgProp.GetArrayElementAtIndex(0).objectReferenceValue = btnBlock.GetComponent<Image>();
        catBgProp.GetArrayElementAtIndex(1).objectReferenceValue = btnRamp.GetComponent<Image>();
        catBgProp.GetArrayElementAtIndex(2).objectReferenceValue = btnTarget.GetComponent<Image>();

        // Inspector
        uiSer.FindProperty("inspectorPanel").objectReferenceValue = inspectorPanel;
        uiSer.FindProperty("sliderRotation").objectReferenceValue = sliderRot;
        uiSer.FindProperty("sliderScale").objectReferenceValue = sliderScale;
        uiSer.FindProperty("txtRotationValue").objectReferenceValue = txtRotVal;
        uiSer.FindProperty("txtScaleValue").objectReferenceValue = txtScaleVal;
        uiSer.FindProperty("btnChangeColor").objectReferenceValue = btnColor;
        uiSer.FindProperty("toggleKinematic").objectReferenceValue = toggleKinematic;
        uiSer.FindProperty("btnDeleteObject").objectReferenceValue = btnDelete;

        // Chaos
        uiSer.FindProperty("chaosPanel").objectReferenceValue = chaosPanel;
        uiSer.FindProperty("btnShoot").objectReferenceValue = btnShoot;
        uiSer.FindProperty("btnShockwave").objectReferenceValue = btnShockwave;
        uiSer.FindProperty("btnZeroG").objectReferenceValue = btnZeroG;
        uiSer.FindProperty("btnBulletTime").objectReferenceValue = btnBulletTime;
        uiSer.FindProperty("imgZeroGHighlight").objectReferenceValue = btnZeroG.GetComponent<Image>();
        uiSer.FindProperty("imgBulletTimeHighlight").objectReferenceValue = btnBulletTime.GetComponent<Image>();

        // Face
        uiSer.FindProperty("facePanel").objectReferenceValue = facePanel;
        uiSer.FindProperty("btnCycleAccessory").objectReferenceValue = btnCycleFace;
        uiSer.FindProperty("btnShootLaser").objectReferenceValue = btnShootLaser;

        uiSer.ApplyModifiedProperties();
    }

    // =========================================================================
    // MÉTODOS DE CREACIÓN DE PREFABS 3D CON MATERIALES GUARDADOS
    // =========================================================================

    private static bool CreateBlockPrefab()
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Block";
        block.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        block.tag = "SpawnedObject";

        Material mat = CreateMaterial("Mat_Block", new Color(0.2f, 0.45f, 0.95f));
        SetRendererMaterial(block, mat);

        Rigidbody rb = block.AddComponent<Rigidbody>();
        rb.mass = 1f;

        bool success = SavePrefab(block, "Block");
        Object.DestroyImmediate(block);
        return success;
    }

    private static bool CreateRampPrefab()
    {
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Ramp";
        ramp.transform.localScale = new Vector3(0.24f, 0.06f, 0.18f);
        ramp.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        ramp.tag = "SpawnedObject";

        Material mat = CreateMaterial("Mat_Ramp", new Color(0.2f, 0.85f, 0.35f));
        SetRendererMaterial(ramp, mat);

        Rigidbody rb = ramp.AddComponent<Rigidbody>();
        rb.mass = 2f;

        bool success = SavePrefab(ramp, "Ramp");
        Object.DestroyImmediate(ramp);
        return success;
    }

    private static bool CreateTargetPrefab()
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        target.name = "Target";
        target.transform.localScale = new Vector3(0.1f, 0.14f, 0.1f);
        target.tag = "SpawnedObject";

        Material redMat = CreateMaterial("Mat_Target", new Color(0.95f, 0.2f, 0.2f));
        SetRendererMaterial(target, redMat);

        GameObject bullseye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullseye.name = "Bullseye";
        bullseye.transform.SetParent(target.transform, false);
        bullseye.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        bullseye.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);

        Material yellowMat = CreateMaterial("Mat_Bullseye", new Color(1f, 0.85f, 0.1f));
        SetRendererMaterial(bullseye, yellowMat);
        Object.DestroyImmediate(bullseye.GetComponent<Collider>());

        Rigidbody rb = target.AddComponent<Rigidbody>();
        rb.mass = 0.5f;

        bool success = SavePrefab(target, "Target");
        Object.DestroyImmediate(target);
        return success;
    }

    private static bool CreateProjectilePrefab()
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Projectile";
        projectile.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);

        Material projMat = CreateMaterial("Mat_Projectile", new Color(1f, 0.45f, 0.05f));
        SetRendererMaterial(projectile, projMat);

        Rigidbody rb = projectile.AddComponent<Rigidbody>();
        rb.mass = 0.4f;

        TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
        trail.startWidth = 0.05f;
        trail.endWidth = 0.005f;
        trail.time = 0.4f;
        Material trailMat = CreateMaterial("Mat_Trail", new Color(1f, 0.7f, 0.1f), true);
        trail.sharedMaterial = trailMat;

        bool success = SavePrefab(projectile, "Projectile");
        Object.DestroyImmediate(projectile);
        return success;
    }

    private static bool CreatePlacementReticlePrefab()
    {
        GameObject reticle = new GameObject("PlacementReticle");

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Ring";
        ring.transform.SetParent(reticle.transform, false);
        ring.transform.localScale = new Vector3(0.18f, 0.003f, 0.18f);
        Object.DestroyImmediate(ring.GetComponent<Collider>());

        Material reticleMat = CreateMaterial("Mat_Reticle", new Color(1f, 1f, 1f, 0.6f), true);
        SetRendererMaterial(ring, reticleMat);

        bool success = SavePrefab(reticle, "PlacementReticle");
        Object.DestroyImmediate(reticle);
        return success;
    }

    private static bool CreatePortalPlatformPrefab()
    {
        GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        portal.name = "PortalPlatform";
        portal.transform.localScale = new Vector3(0.35f, 0.015f, 0.35f);

        Material portalMat = CreateMaterial("Mat_Portal", new Color(0.6f, 0.15f, 0.9f));
        SetRendererMaterial(portal, portalMat);

        Collider col = portal.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        bool success = SavePrefab(portal, "PortalPlatform");
        Object.DestroyImmediate(portal);
        return success;
    }

    private static bool CreateCyberGlassesPrefab()
    {
        GameObject glasses = new GameObject("CyberGlasses");

        Material cyanMat = CreateMaterial("Mat_CyberGlasses", new Color(0.1f, 0.9f, 1f));

        GameObject leftLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftLens.name = "LeftLens";
        leftLens.transform.SetParent(glasses.transform, false);
        leftLens.transform.localPosition = new Vector3(-0.035f, 0f, 0f);
        leftLens.transform.localScale = new Vector3(0.03f, 0.015f, 0.01f);
        SetRendererMaterial(leftLens, cyanMat);
        Object.DestroyImmediate(leftLens.GetComponent<Collider>());

        GameObject rightLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightLens.name = "RightLens";
        rightLens.transform.SetParent(glasses.transform, false);
        rightLens.transform.localPosition = new Vector3(0.035f, 0f, 0f);
        rightLens.transform.localScale = new Vector3(0.03f, 0.015f, 0.01f);
        SetRendererMaterial(rightLens, cyanMat);
        Object.DestroyImmediate(rightLens.GetComponent<Collider>());

        bool success = SavePrefab(glasses, "CyberGlasses");
        Object.DestroyImmediate(glasses);
        return success;
    }

    private static bool CreateCrownPrefab()
    {
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        crown.name = "Crown";
        crown.transform.localScale = new Vector3(0.08f, 0.035f, 0.08f);

        Material goldMat = CreateMaterial("Mat_Crown", new Color(1f, 0.85f, 0.15f));
        SetRendererMaterial(crown, goldMat);
        Object.DestroyImmediate(crown.GetComponent<Collider>());

        bool success = SavePrefab(crown, "Crown");
        Object.DestroyImmediate(crown);
        return success;
    }

    private static bool CreateARDefaultPlanePrefab()
    {
        GameObject arPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        arPlane.name = "ARDefaultPlane";
        arPlane.transform.localScale = Vector3.one;
        arPlane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Material planeMat = CreateMaterial("Mat_ARPlane", new Color(0.85f, 0.85f, 0.85f, 0.45f), true);
        SetRendererMaterial(arPlane, planeMat);

        Object.DestroyImmediate(arPlane.GetComponent<MeshCollider>());

        BoxCollider boxCol = arPlane.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(1f, 0.02f, 1f);
        boxCol.center = new Vector3(0f, -0.01f, 0f);
        boxCol.isTrigger = false;

        bool success = SavePrefab(arPlane, "ARDefaultPlane");
        Object.DestroyImmediate(arPlane);
        return success;
    }

    // =========================================================================
    // UTILITARIOS DE MATERIALES Y UI
    // =========================================================================

    private static Material CreateMaterial(string name, Color color, bool transparent = false)
    {
        EnsureFolderExists(MaterialsFolder);
        string matPath = $"{MaterialsFolder}/{name}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            mat = new Material(shader);
            mat.name = name;
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }

        if (transparent)
        {
            ConfigureTransparentMaterial(mat);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ConfigureTransparentMaterial(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private static void SetRendererMaterial(GameObject go, Material mat)
    {
        if (go == null || mat == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    private static bool SavePrefab(GameObject go, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        return true;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = panel.AddComponent<Image>();
        img.color = color;
        return panel;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 size, Vector2 pos, string text, Color color)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private static TextMeshProUGUI CreateTMP(string name, Transform parent, Vector2 size, Vector2 pos, string text, float fontSize, TextAlignmentOptions align)
    {
        GameObject txtObj = new GameObject(name);
        txtObj.transform.SetParent(parent, false);
        RectTransform rt = txtObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    private static Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 pos, float min, float max)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        RectTransform rt = sliderObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;

        // Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.sizeDelta = new Vector2(-10, 0);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fill.AddComponent<RectTransform>();
        fRt.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.25f, 0.6f, 0.95f);
        slider.fillRect = fRt;

        return slider;
    }

    private static Toggle CreateToggle(string name, Transform parent, Vector2 size, Vector2 pos, string label)
    {
        GameObject toggleObj = new GameObject(name);
        toggleObj.transform.SetParent(parent, false);
        RectTransform rt = toggleObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(toggleObj.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.sizeDelta = new Vector2(30, 30);
        bgRt.anchoredPosition = new Vector2(-120, 0);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.3f);
        toggle.targetGraphic = bgImg;

        GameObject check = new GameObject("Checkmark");
        check.transform.SetParent(bg.transform, false);
        RectTransform cRt = check.AddComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(20, 20);
        Image cImg = check.AddComponent<Image>();
        cImg.color = new Color(0.3f, 0.9f, 0.4f);
        toggle.graphic = cImg;

        GameObject lbl = new GameObject("Label");
        lbl.transform.SetParent(toggleObj.transform, false);
        RectTransform lRt = lbl.AddComponent<RectTransform>();
        lRt.sizeDelta = new Vector2(220, 30);
        lRt.anchoredPosition = new Vector2(20, 0);
        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;

        return toggle;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static void EnsureFolderExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }

    private static void EnsureTagExists(string tagName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        if (tagsProp == null) return;

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t != null && t.stringValue == tagName) return;
        }

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
        if (newTag != null)
        {
            newTag.stringValue = tagName;
            tagManager.ApplyModifiedProperties();
        }
    }
}
