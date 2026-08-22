using UnityEngine;

public enum OfficeDirection
{
    Front,
    Back,
    Left,
    Right
}

[RequireComponent(typeof(SpriteRenderer))]
public sealed class OfficeDirectionalSprite : MonoBehaviour
{
    [SerializeField] private string resourcePath;
    [SerializeField] private OfficeDirection initialDirection = OfficeDirection.Front;
    [SerializeField] private float worldScale = 1.25f;

    private readonly Sprite[] directionSprites = new Sprite[4];
    private SpriteRenderer spriteRenderer;
    private OfficeDirection currentDirection;
    private bool configured;

    public OfficeDirection CurrentDirection => currentDirection;

    public void Configure(string sheetResourcePath, OfficeDirection direction, float scale = 1.25f)
    {
        resourcePath = sheetResourcePath;
        initialDirection = direction;
        worldScale = scale;
        configured = true;
    }

    private void Start()
    {
        if (!configured)
        {
            Debug.LogError("[OfficeMVP] Directional sprite was not configured: " + name);
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        LoadSheet();
        SetDirection(initialDirection);
    }

    public void SetDirectionFromMovement(Vector2 movement)
    {
        if (movement.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
        {
            SetDirection(movement.x < 0f ? OfficeDirection.Left : OfficeDirection.Right);
        }
        else
        {
            SetDirection(movement.y > 0f ? OfficeDirection.Back : OfficeDirection.Front);
        }
    }

    public void SetDirection(OfficeDirection direction)
    {
        currentDirection = direction;
        if (spriteRenderer != null && directionSprites[(int)direction] != null)
        {
            spriteRenderer.sprite = directionSprites[(int)direction];
        }
    }

    private void LoadSheet()
    {
        var sheet = Resources.Load<Texture2D>(resourcePath);
        if (sheet == null)
        {
            Debug.LogError("[OfficeMVP] Direction sheet not found: " + resourcePath);
            return;
        }

        var cellWidth = sheet.width / 4;
        var cellHeight = sheet.height;
        for (var index = 0; index < 4; index++)
        {
            var rect = new Rect(index * cellWidth, 0f, cellWidth, cellHeight);
            directionSprites[index] = Sprite.Create(
                sheet,
                rect,
                new Vector2(0.5f, 0f),
                cellWidth,
                0,
                SpriteMeshType.FullRect
            );
        }

        transform.localScale = Vector3.one * worldScale;
        spriteRenderer.sprite = directionSprites[(int)initialDirection];
        spriteRenderer.sortingOrder = 45;
    }
}
