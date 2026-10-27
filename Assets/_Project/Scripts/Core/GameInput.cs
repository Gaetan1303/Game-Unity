using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Toute la lecture des entrées au même endroit (clavier + manette).</summary>
public static class GameInput
{
    public static Vector2 Move()
    {
        Vector2 m = Vector2.zero;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) m.x -= 1;   // Q sur AZERTY
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) m.x += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) m.y -= 1;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) m.y += 1;     // Z sur AZERTY
        }
        var g = Gamepad.current;
        if (g != null) m += g.leftStick.ReadValue();
        return Vector2.ClampMagnitude(m, 1f);
    }

    static bool Key(System.Func<Keyboard, bool> f) { var k = Keyboard.current; return k != null && f(k); }
    static bool Pad(System.Func<Gamepad, bool> f) { var g = Gamepad.current; return g != null && f(g); }

    // Attaque / artes standard (+ direction)
    public static bool AttackPressed() => Key(k => k.jKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || Pad(g => g.buttonWest.wasPressedThisFrame);
    // Artes techniques (+ direction)
    public static bool TechPressed() => Key(k => k.kKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) || Pad(g => g.buttonEast.wasPressedThisFrame);
    public static bool JumpPressed() => Key(k => k.spaceKey.wasPressedThisFrame) || Pad(g => g.buttonSouth.wasPressedThisFrame);
    public static bool GuardHeld() => Key(k => k.leftCtrlKey.isPressed) || Pad(g => g.leftTrigger.isPressed);
    public static bool FreeRunHeld() => Key(k => k.leftShiftKey.isPressed) || Pad(g => g.leftShoulder.isPressed);
    public static bool SwitchTargetPressed() => Key(k => k.tabKey.wasPressedThisFrame) || Pad(g => g.rightShoulder.wasPressedThisFrame);
    public static bool OverlimitPressed() => Key(k => k.lKey.wasPressedThisFrame) || Pad(g => g.rightTrigger.wasPressedThisFrame);
    public static bool ItemPressed() => Key(k => k.hKey.wasPressedThisFrame) || Pad(g => g.buttonNorth.wasPressedThisFrame);
    public static bool ConfirmPressed() => Key(k => k.enterKey.wasPressedThisFrame || k.jKey.wasPressedThisFrame) || Pad(g => g.buttonSouth.wasPressedThisFrame);
}
