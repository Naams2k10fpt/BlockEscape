using System;
using BlockEscape.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    public sealed class InputRebindMenu : MonoBehaviour
    {
        private static readonly RebindAction[] Targets =
        {
            RebindAction.TetrisLeft,
            RebindAction.TetrisRight,
            RebindAction.TetrisRotate,
            RebindAction.TetrisSoftDrop,
            RebindAction.PlayerLeft,
            RebindAction.PlayerRight,
            RebindAction.PlayerJump,
            RebindAction.PlayerCrouch
        };

        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _bindingButtons;
        [SerializeField] private Text[] _bindingValues;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _backButton;

        private UnityAction[] _bindingListeners;
        private bool _listenersBound;

        public event Action Closed;
        public bool IsVisible => _panel != null && _panel.activeSelf;

        private void OnEnable()
        {
            NeonMenuTheme.ApplyOverlay(_panel);
            BindButtons();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        public void Configure(
            GameObject panel,
            Button[] bindingButtons,
            Text[] bindingValues,
            Button resetButton,
            Button backButton)
        {
            UnbindButtons();
            _panel = panel;
            _bindingButtons = bindingButtons;
            _bindingValues = bindingValues;
            _resetButton = resetButton;
            _backButton = backButton;
            NeonMenuTheme.ApplyOverlay(_panel);
            if (isActiveAndEnabled)
                BindButtons();
        }

        public void Show()
        {
            EnsureInputService();
            RefreshBindings();
            _panel?.SetActive(true);
            if (_bindingButtons is { Length: > 0 })
                _bindingButtons[0]?.Select();
        }

        public void Hide()
        {
            _panel?.SetActive(false);
        }

        private void BeginRebind(int index)
        {
            if (index < 0 || index >= Targets.Length || index >= (_bindingValues?.Length ?? 0))
                return;

            var input = EnsureInputService();
            SetButtonsInteractable(false);
            _bindingValues[index].text = "PRESS A KEY  (ESC CANCEL)";
            if (!input.StartInteractiveRebind(Targets[index], _ =>
                {
                    SetButtonsInteractable(true);
                    RefreshBindings();
                    _bindingButtons[index]?.Select();
                }))
            {
                SetButtonsInteractable(true);
                RefreshBindings();
            }
        }

        private void ResetBindings()
        {
            EnsureInputService().ResetBindingsToDefault();
            RefreshBindings();
        }

        private void Close()
        {
            Hide();
            Closed?.Invoke();
        }

        private void RefreshBindings()
        {
            var input = EnsureInputService();
            var count = Mathf.Min(Targets.Length, _bindingValues?.Length ?? 0);
            for (var i = 0; i < count; i++)
                if (_bindingValues[i] != null)
                    _bindingValues[i].text = input.GetBindingDisplayString(Targets[i]).ToUpperInvariant();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_bindingButtons != null)
                foreach (var button in _bindingButtons)
                    if (button != null) button.interactable = interactable;
            if (_resetButton != null) _resetButton.interactable = interactable;
            if (_backButton != null) _backButton.interactable = interactable;
        }

        private static InputService EnsureInputService()
        {
            var input = InputService.Current;
            if (input == null)
                input = new GameObject("Input Service (Persistent)").AddComponent<InputService>();
            input.EnsureInitialized();
            return input;
        }

        private void BindButtons()
        {
            if (_listenersBound)
                return;

            var count = Mathf.Min(Targets.Length, _bindingButtons?.Length ?? 0);
            _bindingListeners = new UnityAction[count];
            for (var i = 0; i < count; i++)
            {
                var index = i;
                _bindingListeners[i] = () => BeginRebind(index);
                _bindingButtons[i]?.onClick.AddListener(_bindingListeners[i]);
            }

            _resetButton?.onClick.AddListener(ResetBindings);
            _backButton?.onClick.AddListener(Close);
            _listenersBound = true;
        }

        private void UnbindButtons()
        {
            if (!_listenersBound)
                return;

            var count = Mathf.Min(_bindingListeners?.Length ?? 0, _bindingButtons?.Length ?? 0);
            for (var i = 0; i < count; i++)
                _bindingButtons[i]?.onClick.RemoveListener(_bindingListeners[i]);
            _resetButton?.onClick.RemoveListener(ResetBindings);
            _backButton?.onClick.RemoveListener(Close);
            _bindingListeners = null;
            _listenersBound = false;
        }
    }
}
