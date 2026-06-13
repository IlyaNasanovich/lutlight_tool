using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LutLight2D
{
    [RequireComponent(typeof(UIDocument))]
    public class LutLightToolUI : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private Texture2D _spriteAtlas;
        private List<Color> _uniqueColors = new List<Color>();
        private List<List<Color>> _colorPallet = new List<List<Color>>();
        private int _shadowDegree = 2;
        private bool _shadowDegreeEnabled = true;

        // UI Elements
        private Button _uploadButton;
        private Toggle _shadowDegreeToggle;
        private TextField _shadowDegreeInput;
        private Label _infoLabel;
        private VisualElement _previewContainer;
        private VisualElement _palletContainer;
        private Label _waitingLabel;

        private void Awake()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            // Ensure the UIDocument has a visual tree
            if (_uiDocument.visualTreeAsset == null)
            {
                Debug.LogError("UIDocument has no visual tree asset assigned!");
            }
        }

        private void OnEnable()
        {
            // Wait a frame for the UIDocument to initialize
            StartCoroutine(InitializeUI());
        }

        private System.Collections.IEnumerator InitializeUI()
        {
            yield return null; // Wait one frame

            var root = _uiDocument.rootVisualElement;

            // Debug: Print all elements with names
            Debug.Log($"Root child count: {root.childCount}");
            foreach (var child in root.Children())
            {
                Debug.Log($"Child: {child.name} ({child.GetType().Name})");
            }

            // Get UI elements
            _uploadButton = root.Q<Button>("upload-button");
            _shadowDegreeToggle = root.Q<Toggle>("shadow-degree-toggle");
            _shadowDegreeInput = root.Q<TextField>("shadow-degree-input");
            _infoLabel = root.Q<Label>("info-label");
            _previewContainer = root.Q<VisualElement>("preview-container");
            _palletContainer = root.Q<VisualElement>("pallet-container");
            _waitingLabel = root.Q<Label>("waiting-label");

            // Debug: Check which elements are null
            Debug.Log($"upload-button: {_uploadButton != null}");
            Debug.Log($"shadow-degree-toggle: {_shadowDegreeToggle != null}");
            Debug.Log($"shadow-degree-input: {_shadowDegreeInput != null}");
            Debug.Log($"info-label: {_infoLabel != null}");
            Debug.Log($"preview-container: {_previewContainer != null}");
            Debug.Log($"pallet-container: {_palletContainer != null}");
            Debug.Log($"waiting-label: {_waitingLabel != null}");

            // Register callbacks only if elements exist
            if (_uploadButton != null)
                _uploadButton.RegisterCallback<ClickEvent>(OnUploadClicked);
            else
                Debug.LogError("upload-button not found!");

            if (_shadowDegreeToggle != null)
                _shadowDegreeToggle.RegisterCallback<ChangeEvent<bool>>(OnShadowDegreeToggleChanged);
            else
                Debug.LogError("shadow-degree-toggle not found!");

            if (_shadowDegreeInput != null)
            {
                _shadowDegreeInput.RegisterCallback<ChangeEvent<string>>(OnShadowDegreeInputChanged);
                // Initialize
                _shadowDegreeInput.value = "2";
            }
            else
                Debug.LogError("shadow-degree-input not found!");

            if (_shadowDegreeToggle != null)
                _shadowDegreeToggle.value = true;
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            if (_uploadButton != null)
                _uploadButton.UnregisterCallback<ClickEvent>(OnUploadClicked);
            if (_shadowDegreeToggle != null)
                _shadowDegreeToggle.UnregisterCallback<ChangeEvent<bool>>(OnShadowDegreeToggleChanged);
            if (_shadowDegreeInput != null)
                _shadowDegreeInput.UnregisterCallback<ChangeEvent<string>>(OnShadowDegreeInputChanged);
        }

        private void OnUploadClicked(ClickEvent evt)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Sprite Atlas", "", "png");
            if (!string.IsNullOrEmpty(path))
            {
                LoadSpriteAtlas(path);
            }
#else
            ShowFilePathDialog();
#endif
        }

#if !UNITY_EDITOR
        private void ShowFilePathDialog()
        {
            var root = _uiDocument.rootVisualElement;

            var dialog = new VisualElement();
            dialog.name = "file-dialog";
            dialog.style.position = Position.Absolute;
            dialog.style.left = 0;
            dialog.style.top = 0;
            dialog.style.right = 0;
            dialog.style.bottom = 0;
            dialog.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.8f));
            dialog.style.justifyContent = Justify.Center;
            dialog.style.alignItems = Align.Center;

            var content = new VisualElement();
            content.style.width = 400;
            content.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            content.style.paddingTop = 20;
            content.style.paddingBottom = 20;
            content.style.paddingLeft = 20;
            content.style.paddingRight = 20;
            content.style.borderTopLeftRadius = 8;
            content.style.borderTopRightRadius = 8;
            content.style.borderBottomLeftRadius = 8;
            content.style.borderBottomRightRadius = 8;

            var titleLabel = new Label("Enter PNG file path:");
            titleLabel.style.color = Color.white;
            titleLabel.style.fontSize = 14;
            titleLabel.style.marginBottom = 10;
            content.Add(titleLabel);

            var input = new TextField();
            input.value = "";
            content.Add(input);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 15;
            buttonRow.style.gap = 10;

            var okButton = new Button(() =>
            {
                string path = input.value;
                root.Remove(dialog);
                if (!string.IsNullOrEmpty(path))
                {
                    LoadSpriteAtlas(path);
                }
            });
            okButton.text = "OK";
            okButton.style.width = 80;
            buttonRow.Add(okButton);

            var cancelButton = new Button(() => root.Remove(dialog));
            cancelButton.text = "Cancel";
            cancelButton.style.width = 80;
            buttonRow.Add(cancelButton);

            content.Add(buttonRow);
            dialog.Add(content);

            root.Add(dialog);
        }
#endif

        private void OnShadowDegreeToggleChanged(ChangeEvent<bool> evt)
        {
            _shadowDegreeEnabled = evt.newValue;
            _shadowDegreeInput.SetEnabled(_shadowDegreeEnabled);

            if (_shadowDegreeEnabled && _spriteAtlas != null)
            {
                if (int.TryParse(_shadowDegreeInput.value, out int degree) && degree >= 2)
                {
                    _shadowDegree = degree;
                    GenerateColorPallet();
                }
            }
        }

        private void OnShadowDegreeInputChanged(ChangeEvent<string> evt)
        {
            if (!_shadowDegreeEnabled || _spriteAtlas == null) return;

            if (int.TryParse(evt.newValue, out int degree) && degree >= 2)
            {
                _shadowDegree = degree;
                GenerateColorPallet();
            }
            else
            {
                _infoLabel.text = "Must be a number >= 2";
            }
        }

        private void LoadSpriteAtlas(string path)
        {
            byte[] fileData = File.ReadAllBytes(path);
            _spriteAtlas = new Texture2D(2, 2);
            _spriteAtlas.LoadImage(fileData);
            _spriteAtlas.filterMode = FilterMode.Point;

            // Hide waiting label, show preview
            _waitingLabel.style.display = DisplayStyle.None;

            // Create preview image
            _previewContainer.Clear();
            var previewImage = new Image();
            previewImage.image = _spriteAtlas;
            previewImage.scaleMode = ScaleMode.ScaleToFit;
            _previewContainer.Add(previewImage);

            GenerateColorPallet();
        }

        private void GenerateColorPallet()
        {
            if (_spriteAtlas == null) return;

            // Get all unique colors accounting for alpha channel
            _uniqueColors = GetUniqueColors(_spriteAtlas);
            
            var temp = _uniqueColors.Select(c => 
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                return (color: c, h, s, v);
            }).ToList();

            // Sort by R, G, B, A
            _uniqueColors = temp
                .OrderBy(x => x.h)
                .ThenBy(x => x.s)
                .ThenBy(x => x.v)
                .ThenBy(x => x.color.a)
                .Select(x => x.color)
                .ToList();

            // Generate color pallet
            _colorPallet = new List<List<Color>>();

            // Add base level (level 0) - read only
            _colorPallet.Add(new List<Color>(_uniqueColors));

            // Add shadow levels
            int shadowLevels = _shadowDegreeEnabled ? _shadowDegree : 1;
            for (int i = 1; i < shadowLevels; i++)
            {
                var level = new List<Color>();
                foreach (var color in _uniqueColors)
                {
                    // Darken color for shadow levels
                    float factor = 1f - (float)i / shadowLevels;
                    level.Add(new Color(color.r * factor, color.g * factor, color.b * factor, color.a));
                }
                _colorPallet.Add(level);
            }

            // Update info label
            _infoLabel.text = $"Colors: {_uniqueColors.Count} | Levels: {_colorPallet.Count}";

            // Draw color pallet
            DrawColorPallet();
        }

        private List<Color> GetUniqueColors(Texture2D texture)
        {
            HashSet<Color> uniqueColors = new HashSet<Color>();
            Color[] pixels = texture.GetPixels();

            foreach (var pixel in pixels)
            {
                // Only add non-transparent pixels
                if (pixel.a > 0.01f)
                {
                    uniqueColors.Add(pixel);
                }
            }

            return uniqueColors.ToList();
        }

        private void DrawColorPallet()
        {
            _palletContainer.Clear();

            if (_colorPallet.Count == 0) return;

            int width = _colorPallet[0].Count;
            int height = _colorPallet.Count;

            // Limit display to avoid performance issues
            int maxDisplayWidth = 64;
            int maxDisplayHeight = 32;
            int displayWidth = Mathf.Min(width, maxDisplayWidth);
            int displayHeight = Mathf.Min(height, maxDisplayHeight);

            for (int y = 0; y < displayHeight; y++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                for (int x = 0; x < displayWidth; x++)
                {
                    if (x < _colorPallet[y].Count)
                    {
                        var cell = new VisualElement();
                        cell.style.width = 12;
                        cell.style.height = 12;
                        cell.style.backgroundColor = new StyleColor(_colorPallet[y][x]);
                        row.Add(cell);
                    }
                }

                _palletContainer.Add(row);
            }

            if (width > maxDisplayWidth || height > maxDisplayHeight)
            {
                var note = new Label($"Showing {displayWidth}x{displayHeight} of {width}x{height}");
                note.style.fontSize = 10;
                _palletContainer.Add(note);
            }
        }
    }
}
