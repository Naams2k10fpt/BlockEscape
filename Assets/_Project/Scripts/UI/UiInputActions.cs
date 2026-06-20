using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BlockEscape.UI
{
    public static class UiInputActions
    {
        public static void AssignTo(InputSystemUIInputModule module)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Block Escape UI Actions";
            var ui = asset.AddActionMap("UI");

            var point = ui.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position");

            var navigate = ui.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            var submit = ui.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");

            var cancel = ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            var click = ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
            var rightClick = ui.AddAction("RightClick", InputActionType.PassThrough, "<Mouse>/rightButton");
            var middleClick = ui.AddAction("MiddleClick", InputActionType.PassThrough, "<Mouse>/middleButton");
            var scroll = ui.AddAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll", expectedControlLayout: "Vector2");

            module.actionsAsset = asset;
            module.point = InputActionReference.Create(point);
            module.move = InputActionReference.Create(navigate);
            module.submit = InputActionReference.Create(submit);
            module.cancel = InputActionReference.Create(cancel);
            module.leftClick = InputActionReference.Create(click);
            module.rightClick = InputActionReference.Create(rightClick);
            module.middleClick = InputActionReference.Create(middleClick);
            module.scrollWheel = InputActionReference.Create(scroll);
        }
    }
}
