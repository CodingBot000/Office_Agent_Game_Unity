using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement2D : MonoBehaviour
{
    [SerializeField] private float speed = 4f;

    private Rigidbody2D body;
    private OfficeDirectionalSprite directionalSprite;
    private Vector2 input;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        directionalSprite = GetComponent<OfficeDirectionalSprite>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        if (OfficeBackendClient.Instance != null && !OfficeBackendClient.Instance.IsReady)
        {
            input = Vector2.zero;
            return;
        }

        if (IsTextInputFocused())
        {
            input = Vector2.zero;
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            input = Vector2.zero;
            return;
        }

        input = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;

        input = input.normalized;
        if (directionalSprite != null && input.sqrMagnitude > 0.0001f)
        {
            directionalSprite.SetDirectionFromMovement(input);
        }
    }

    private static bool IsTextInputFocused()
    {
        var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (selected == null)
        {
            return false;
        }

        return selected.GetComponent<InputField>() != null || selected.GetComponentInParent<InputField>() != null;
    }

    private void FixedUpdate()
    {
        body.MovePosition(body.position + input * speed * Time.fixedDeltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("[OfficeMVP] Player collision enter: " + collision.collider.name);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Debug.Log("[OfficeMVP] Player collision exit: " + collision.collider.name);
    }
}
