using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public sealed class OfficeMvpBootstrap : MonoBehaviour
{
    private Sprite pixelSprite;
    private Sprite circleSprite;
    private Sprite floorSprite;
    private Sprite deskSprite;
    private Sprite monitorSprite;
    private Sprite keyboardSprite;
    private Sprite chairSprite;
    private Sprite partitionSprite;
    private Sprite serverRackSprite;
    private Sprite whiteboardSprite;
    private Sprite coffeeMachineSprite;
    private Transform mapRoot;
    private Transform obstacleRoot;
    private Transform decorationRoot;
    private Transform interactableRoot;
    private Transform locationZoneRoot;

    private readonly Color floorColor = new Color(0.42f, 0.30f, 0.20f);
    private readonly Color devColor = new Color(0.13f, 0.42f, 0.36f, 0.42f);
    private readonly Color qaColor = new Color(0.82f, 0.58f, 0.08f, 0.42f);
    private readonly Color pmColor = new Color(0.16f, 0.37f, 0.58f, 0.42f);
    private readonly Color wallColor = new Color(0.15f, 0.13f, 0.16f);
    private readonly Color partitionColor = new Color(0.23f, 0.58f, 0.52f);
    private readonly Color deskColor = new Color(0.68f, 0.45f, 0.25f);
    private readonly Color monitorColor = new Color(0.08f, 0.10f, 0.14f);
    private readonly Color interactionColor = new Color(1.00f, 0.38f, 0.12f);
    private const float FurnitureScale = 2f;
    private const float CharacterScale = 2f;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        pixelSprite = CreatePixelSprite();
        circleSprite = CreateCircleSprite();
        LoadArt();

        mapRoot = CreateRoot("Map");
        obstacleRoot = CreateRoot("Obstacles");
        decorationRoot = CreateRoot("Decorations");
        interactableRoot = CreateRoot("InteractablePoints");
        locationZoneRoot = CreateRoot("LocationZones");

        ConfigureCamera();
        BuildMap();
        BuildLocationZones();
        BuildPlayer();
        BuildBackendClient();
        BuildWorldObjectPresenter();
        BuildInteractionUI();

        Debug.Log("[OfficeMVP] Three-zone office blockout initialized.");
    }

    private void LoadArt()
    {
        floorSprite = Resources.Load<Sprite>("OfficeMVP/floor_tile");
        deskSprite = Resources.Load<Sprite>("OfficeMVP/desk");
        monitorSprite = Resources.Load<Sprite>("OfficeMVP/monitor");
        keyboardSprite = Resources.Load<Sprite>("OfficeMVP/keyboard");
        chairSprite = Resources.Load<Sprite>("OfficeMVP/chair");
        partitionSprite = Resources.Load<Sprite>("OfficeMVP/partition");
        serverRackSprite = Resources.Load<Sprite>("OfficeMVP/server_rack");
        whiteboardSprite = Resources.Load<Sprite>("OfficeMVP/whiteboard");
        coffeeMachineSprite = Resources.Load<Sprite>("OfficeMVP/coffee_machine");

        Debug.Log($"[OfficeMVP] Art loaded: floor={floorSprite != null}, desk={deskSprite != null}, monitor={monitorSprite != null}, keyboard={keyboardSprite != null}, chair={chairSprite != null}, partition={partitionSprite != null}");
    }

    private Transform CreateRoot(string rootName)
    {
        var root = new GameObject(rootName).transform;
        root.SetParent(transform, false);
        return root;
    }

    private Sprite CreatePixelSprite()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "OfficeMVP_PixelTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private Sprite CreateCircleSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "OfficeMVP_CircleTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var center = (size - 1) * 0.5f;
        var radius = size * 0.46f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void ConfigureCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 7.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.transform.rotation = Quaternion.identity;
    }

    private void CreateFloorTiles()
    {
        const int columns = 5;
        const int rows = 3;
        const float tileSize = 4f;

        for (var x = 0; x < columns; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                var position = new Vector2(-10f + (x + 0.5f) * tileSize, -6f + (y + 0.5f) * tileSize);
                CreateSpriteRect($"FloorTile_{x}_{y}", position, new Vector2(tileSize, tileSize), floorSprite, -20, mapRoot, false, false);
            }
        }
    }

    private void BuildMap()
    {
        CreateFloorTiles();

        CreateRect("DevArea", new Vector2(-5.5f, 0f), new Vector2(7.5f, 10.5f), devColor, -10, mapRoot, false);
        CreateRect("QaArea", new Vector2(5.0f, 3.0f), new Vector2(5.8f, 4.4f), qaColor, -10, mapRoot, false);
        CreateRect("PmArea", new Vector2(5.0f, -3.0f), new Vector2(5.8f, 4.4f), pmColor, -10, mapRoot, false);

        CreateObstacle("NorthWall", new Vector2(0f, 6.1f), new Vector2(20.5f, 0.35f), wallColor);
        CreateObstacle("SouthWall", new Vector2(0f, -6.1f), new Vector2(20.5f, 0.35f), wallColor);
        CreateObstacle("WestWall", new Vector2(-10.1f, 0f), new Vector2(0.35f, 12f), wallColor);
        CreateObstacle("EastWall", new Vector2(10.1f, 0f), new Vector2(0.35f, 12f), wallColor);

        CreateSpriteRect("DevScreenNorth", new Vector2(-8.2f, 4.65f), new Vector2(2.4f, 0.62f), partitionSprite, 20, obstacleRoot, true, false);
        CreateSpriteRect("DevScreenSouth", new Vector2(-8.2f, -4.65f), new Vector2(2.4f, 0.62f), partitionSprite, 20, obstacleRoot, true, false);
        CreateSpriteRect("QaScreen", new Vector2(5.0f, 5.15f), new Vector2(4.5f, 0.62f), partitionSprite, 20, obstacleRoot, true, false);
        CreateSpriteRect("PmScreen", new Vector2(5.0f, -5.15f), new Vector2(4.5f, 0.62f), partitionSprite, 20, obstacleRoot, true, false);

        CreateDesk("BackendDesk", new Vector2(-6.0f, 2.5f), new Color(0.68f, 0.45f, 0.25f));
        CreateDesk("FrontendDesk", new Vector2(-6.0f, -0.1f), new Color(0.68f, 0.45f, 0.25f));
        CreateDesk("SharedDevDesk", new Vector2(-6.0f, -2.7f), new Color(0.68f, 0.45f, 0.25f));

        CreateDesk("QaDesk", new Vector2(5.0f, 3.25f), new Color(0.78f, 0.52f, 0.15f));
        CreateDesk("PmDesk", new Vector2(5.0f, -3.25f), new Color(0.32f, 0.49f, 0.68f));

        CreateSpriteRect("ServerRack", new Vector2(-9.25f, 0.0f), new Vector2(0.7f, 2.0f), serverRackSprite, 25, obstacleRoot, true, false);
        CreateSpriteRect("Whiteboard", new Vector2(-1.7f, 4.75f), new Vector2(0.85f, 1.35f), whiteboardSprite, 25, decorationRoot, true, false);
        CreateSpriteRect("CoffeeMachine", new Vector2(8.7f, 0.2f), new Vector2(0.7f, 0.75f), coffeeMachineSprite, 25, decorationRoot, true, false);

        CreateInteractable("BackendPoint", new Vector2(-6.0f, 2.5f - 0.72f * FurnitureScale), "backend_01", "Backend Developer");
        CreateInteractable("FrontendPoint", new Vector2(-6.0f, -0.1f - 0.72f * FurnitureScale), "frontend_01", "Frontend Developer");
        CreateInteractable("QaPoint", new Vector2(5.0f, 3.25f - 0.72f * FurnitureScale), "qa_01", "QA Engineer");
        CreateInteractable("PmPoint", new Vector2(5.0f, -3.25f - 0.72f * FurnitureScale), "pm_01", "PM / Planner");
    }

    private void BuildLocationZones()
    {
        CreateLocationZone("MeetingRoomZone", new Vector2(0f, 0f), new Vector2(4f, 12f), "meeting_room");
        CreateLocationZone("DevAreaZone", new Vector2(-5.5f, 0f), new Vector2(7.2f, 10.3f), "dev_area");
        CreateLocationZone("QaDeskZone", new Vector2(5f, 3f), new Vector2(5.5f, 4.1f), "qa_desk");
        CreateLocationZone("PmDeskZone", new Vector2(5f, -3f), new Vector2(5.5f, 4.1f), "pm_desk");
    }

    private void CreateLocationZone(string objectName, Vector2 position, Vector2 size, string location)
    {
        var zone = new GameObject(objectName);
        zone.transform.SetParent(locationZoneRoot, false);
        zone.transform.localPosition = new Vector3(position.x, position.y, 0f);
        zone.transform.localScale = new Vector3(size.x, size.y, 1f);

        var collider = zone.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = Vector2.one;

        var locationZone = zone.AddComponent<OfficeLocationZone>();
        locationZone.Configure(location);
    }

    private void BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.SetParent(mapRoot, false);
        player.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        var spriteRenderer = player.AddComponent<SpriteRenderer>();
        spriteRenderer.color = Color.white;
        var directionalSprite = player.AddComponent<OfficeDirectionalSprite>();
        directionalSprite.Configure("OfficeMVP/Characters/Player/sheet", OfficeDirection.Front, 1.15f * CharacterScale);
        CreateWorldLabel(player.transform, "PLAYER");

        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = player.AddComponent<CircleCollider2D>();
        collider.radius = 0.45f;

        var interactionCollider = player.AddComponent<CircleCollider2D>();
        interactionCollider.isTrigger = true;
        interactionCollider.radius = 1.6f;

        player.AddComponent<PlayerMovement2D>();
        player.AddComponent<PlayerInteractionDetector>();
    }

    private void BuildBackendClient()
    {
        var backendObject = new GameObject("BackendClient");
        backendObject.transform.SetParent(transform, false);
        backendObject.AddComponent<OfficeBackendClient>();
    }

    private void BuildWorldObjectPresenter()
    {
        gameObject.AddComponent<OfficeWorldObjectStatePresenter>();
        gameObject.AddComponent<OfficeThrowCoordinator>();
    }

    private void BuildInteractionUI()
    {
        var uiObject = new GameObject("InteractionUI");
        uiObject.AddComponent<OfficeInteractionUI>();
        uiObject.AddComponent<OfficePlayerActionUI>();
        uiObject.AddComponent<OfficeInventoryUI>();
        uiObject.AddComponent<OfficeBackendHealthUI>();
    }

    private GameObject CreateRect(string objectName, Vector2 position, Vector2 size, Color color, int order, Transform parent, bool addCollider)
    {
        var gameObject = new GameObject(objectName);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = new Vector3(position.x, position.y, 0f);
        gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = pixelSprite;
        renderer.color = color;
        renderer.sortingOrder = order;

        if (addCollider)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
        }

        return gameObject;
    }

    private GameObject CreateObstacle(string objectName, Vector2 position, Vector2 size, Color color)
    {
        return CreateRect(objectName, position, size, color, 20, obstacleRoot, true);
    }

    private void CreateDesk(string deskName, Vector2 position, Color color)
    {
        CreateSpriteRect(deskName, position, new Vector2(2.5f, 0.85f) * FurnitureScale, deskSprite, 20, obstacleRoot, true, false);
        CreateSpriteRect($"{deskName}_Monitor", position + new Vector2(0f, 0.28f * FurnitureScale), new Vector2(0.85f, 0.55f) * FurnitureScale, monitorSprite, 25, decorationRoot, false, false);
        var keyboard = CreateSpriteRect($"{deskName}_Keyboard", position + new Vector2(0f, -0.20f * FurnitureScale), new Vector2(0.65f, 0.24f) * FurnitureScale, keyboardSprite, 25, decorationRoot, false, false);
        var objectId = ResolveDeskKeyboardObjectId(deskName);
        if (!string.IsNullOrEmpty(objectId))
        {
            var objectView = keyboard.AddComponent<OfficeWorldObjectView>();
            objectView.Configure(objectId, ResolveDeskLocation(deskName));
        }

        var chair = CreateSpriteRect($"{deskName}_Chair", position + new Vector2(0f, -0.72f * FurnitureScale), new Vector2(0.80f, 0.80f) * FurnitureScale, chairSprite, 18, decorationRoot, false, false);
        chair.SetActive(false); // Temporarily hidden; keep the object and seat position for later restoration.
    }

    private string ResolveDeskLocation(string deskName)
    {
        if (deskName == "BackendDesk" || deskName == "FrontendDesk" || deskName == "SharedDevDesk")
        {
            return "dev_area";
        }

        if (deskName == "QaDesk")
        {
            return "qa_desk";
        }

        return "pm_desk";
    }

    private string ResolveDeskKeyboardObjectId(string deskName)
    {
        switch (deskName)
        {
            case "BackendDesk":
                return "backend_keyboard";
            case "FrontendDesk":
                return "frontend_keyboard";
            case "QaDesk":
                return "qa_keyboard";
            case "PmDesk":
                return "pm_keyboard";
            default:
                return string.Empty;
        }
    }

    private GameObject CreateSpriteRect(string objectName, Vector2 position, Vector2 size, Sprite sprite, int order, Transform parent, bool addCollider, bool tiled)
    {
        var gameObject = new GameObject(objectName);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = new Vector3(position.x, position.y, 0f);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : pixelSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = order;

        if (tiled)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = size;
        }
        else
        {
            var spriteSize = renderer.sprite.bounds.size;
            var width = spriteSize.x <= 0f ? size.x : size.x / spriteSize.x;
            var height = spriteSize.y <= 0f ? size.y : size.y / spriteSize.y;
            gameObject.transform.localScale = new Vector3(width, height, 1f);
        }

        if (addCollider)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
        }

        return gameObject;
    }

    private void CreateInteractable(string objectName, Vector2 position, string targetId, string displayName)
    {
        var character = new GameObject(objectName);
        character.transform.SetParent(interactableRoot, false);
        character.transform.localPosition = new Vector3(position.x, position.y, 0f);

        var spriteRenderer = character.AddComponent<SpriteRenderer>();
        spriteRenderer.color = Color.white;
        var directionalSprite = character.AddComponent<OfficeDirectionalSprite>();
        directionalSprite.Configure(GetCharacterSheetPath(targetId), OfficeDirection.Back, 1.10f * CharacterScale);
        CreateWorldLabel(character.transform, displayName);
        var emotionLabel = character.AddComponent<OfficeCharacterEmotionLabel>();
        emotionLabel.Configure(targetId);

        var interactable = character.AddComponent<InteractablePoint>();
        interactable.Configure(targetId, displayName, ResolveNpcLocation(targetId));

        var solidCollider = character.AddComponent<CapsuleCollider2D>();
        solidCollider.direction = CapsuleDirection2D.Vertical;
        solidCollider.size = new Vector2(0.42f, 0.68f);
        solidCollider.offset = new Vector2(0f, 0.34f);

        var trigger = character.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 1.5f;

        character.AddComponent<OfficeNpcFallView>();
        character.AddComponent<OfficeNpcFearShake>();
    }

    private string ResolveNpcLocation(string targetId)
    {
        if (targetId == "backend_01" || targetId == "frontend_01")
        {
            return "dev_area";
        }

        if (targetId == "qa_01")
        {
            return "qa_desk";
        }

        return "pm_desk";
    }

    private string GetCharacterSheetPath(string targetId)
    {
        if (targetId == "backend_01") return "OfficeMVP/Characters/Backend/sheet";
        if (targetId == "frontend_01") return "OfficeMVP/Characters/Frontend/sheet";
        if (targetId == "qa_01") return "OfficeMVP/Characters/QA/sheet";
        return "OfficeMVP/Characters/PM/sheet";
    }

    private void CreateWorldLabel(Transform parent, string label)
    {
        var canvasObject = new GameObject("RoleLabel");
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        canvasObject.transform.localScale = Vector3.one * (0.01f / CharacterScale);

        var labelCanvas = canvasObject.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;
        labelCanvas.overrideSorting = true;
        labelCanvas.sortingOrder = 200;

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260f, 36f);

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        var shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }
}
