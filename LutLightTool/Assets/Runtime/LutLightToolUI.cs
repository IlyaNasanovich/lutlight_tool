using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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

        // Navigation state
        private int _selectedColumn = 0;
        private int _selectedRow = 0;

        // UI Elements
        private Button _uploadButton;
        private Toggle _shadowDegreeToggle;
        private IntegerField _shadowDegreeInput;
        private Label _infoLabel;
        private VisualElement _previewContainer;
        private VisualElement _palletContainer;
        private Label _waitingLabel;

        // Color editor elements
        private VisualElement _selectedColorPreview;
        private IntegerField _rInput, _gInput, _bInput, _aInput;
        private Button _interpolateButton;
        private Label _positionLabel;

        // Color picker state
        private VisualElement _pickerOverlay;
        private float _pickerH;
        private float _pickerS;
        private float _pickerV;
        private float _pickerA;
        private Texture2D _svTexture;
        private Texture2D _hueTexture;
        private Texture2D _alphaTexture;
        private VisualElement _svPicker;
        private VisualElement _hueSlider;
        private VisualElement _alphaSlider;
        private VisualElement _pickerPreview;
        private IntegerField _pickerRInput, _pickerGInput, _pickerBInput, _pickerAInput;
        private bool _updatingPickerInputs;

        // Bake and light
        private Button _bakeButton;
        private Button _downloadButton;
        private Button _restoreButton;
        private Slider _lightIntensitySlider;
        private Label _lightIntensityValue;
        private GameObject _spriteObject;
        private Light2D _pointLight;
        private Material _lutMaterial;
        private Texture2D _lutTexture;
        private bool _isDraggingLight;

        // Pan and zoom state
        private Vector2 _panOffset = Vector2.zero;
        private float _zoomLevel = 1f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 4f;
        private const float PanStep = 5f;
        private const float ZoomStep = 0.25f;

        private void Awake()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument.visualTreeAsset == null)
            {
                Debug.LogError("UIDocument has no visual tree asset assigned!");
            }
        }

        private void OnEnable()
        {
            StartCoroutine(InitializeUI());
        }

        private System.Collections.IEnumerator InitializeUI()
        {
            yield return null;

            var root = _uiDocument.rootVisualElement;

            // Get UI elements
            _uploadButton = root.Q<Button>("upload-button");
            _shadowDegreeToggle = root.Q<Toggle>("shadow-degree-toggle");
            _shadowDegreeInput = root.Q<IntegerField>("shadow-degree-input");
            _infoLabel = root.Q<Label>("info-label");
            _previewContainer = root.Q<VisualElement>("preview-container");
            _palletContainer = root.Q<VisualElement>("pallet-container");
            _waitingLabel = root.Q<Label>("waiting-label");

            // Color editor elements
            _selectedColorPreview = root.Q<VisualElement>("selected-color-preview");
            _rInput = root.Q<IntegerField>("r-input");
            _gInput = root.Q<IntegerField>("g-input");
            _bInput = root.Q<IntegerField>("b-input");
            _aInput = root.Q<IntegerField>("a-input");
            _interpolateButton = root.Q<Button>("interpolate-button");
            _positionLabel = root.Q<Label>("position-label");

            // Register callbacks
            if (_uploadButton != null)
                _uploadButton.RegisterCallback<ClickEvent>(OnUploadClicked);

            if (_shadowDegreeToggle != null)
                _shadowDegreeToggle.RegisterCallback<ChangeEvent<bool>>(OnShadowDegreeToggleChanged);

            if (_shadowDegreeInput != null)
            {
                _shadowDegreeInput.RegisterCallback<ChangeEvent<int>>(OnShadowDegreeInputChanged);
                _shadowDegreeInput.value = 2;
            }

            // Color input callbacks
            if (_rInput != null) _rInput.RegisterCallback<ChangeEvent<int>>(e => OnColorInputChanged());
            if (_gInput != null) _gInput.RegisterCallback<ChangeEvent<int>>(e => OnColorInputChanged());
            if (_bInput != null) _bInput.RegisterCallback<ChangeEvent<int>>(e => OnColorInputChanged());
            if (_aInput != null) _aInput.RegisterCallback<ChangeEvent<int>>(e => OnColorInputChanged());

            // Interpolate callback
            if (_interpolateButton != null)
                _interpolateButton.RegisterCallback<ClickEvent>(OnInterpolateClicked);

            // Color picker callback
            if (_selectedColorPreview != null)
                _selectedColorPreview.RegisterCallback<ClickEvent>(OnColorPreviewClicked);

            // Bake and light callbacks
            _bakeButton = root.Q<Button>("bake-button");
            _lightIntensitySlider = root.Q<Slider>("light-intensity-slider");
            _lightIntensityValue = root.Q<Label>("light-intensity-value");

            if (_bakeButton != null)
                _bakeButton.RegisterCallback<ClickEvent>(OnBakeClicked);

            // Download and Restore callbacks
            _downloadButton = root.Q<Button>("download-button");
            _restoreButton = root.Q<Button>("restore-button");

            if (_downloadButton != null)
                _downloadButton.RegisterCallback<ClickEvent>(OnDownloadClicked);

            if (_restoreButton != null)
                _restoreButton.RegisterCallback<ClickEvent>(OnRestoreClicked);

            if (_lightIntensitySlider != null)
                _lightIntensitySlider.RegisterCallback<ChangeEvent<float>>(OnLightIntensityChanged);

            // Initialize
            if (_shadowDegreeToggle != null)
                _shadowDegreeToggle.value = true;

            // Register keyboard input in TrickleDown phase so it fires on root
            // regardless of which child element currently has focus
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.focusable = true;

            if (_previewContainer != null)
                _previewContainer.RegisterCallback<GeometryChangedEvent>(OnPreviewGeometryChanged);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            CloseColorPicker();
            DestroySceneObjects();

            if (_lutMaterial != null) { Destroy(_lutMaterial); _lutMaterial = null; }
            if (_lutTexture != null) { Destroy(_lutTexture); _lutTexture = null; }

            if (_uploadButton != null)
                _uploadButton.UnregisterCallback<ClickEvent>(OnUploadClicked);
            if (_shadowDegreeToggle != null)
                _shadowDegreeToggle.UnregisterCallback<ChangeEvent<bool>>(OnShadowDegreeToggleChanged);
            if (_shadowDegreeInput != null)
                _shadowDegreeInput.UnregisterCallback<ChangeEvent<int>>(OnShadowDegreeInputChanged);
            if (_downloadButton != null)
                _downloadButton.UnregisterCallback<ClickEvent>(OnDownloadClicked);
            if (_restoreButton != null)
                _restoreButton.UnregisterCallback<ClickEvent>(OnRestoreClicked);
            if (_previewContainer != null)
                _previewContainer.UnregisterCallback<GeometryChangedEvent>(OnPreviewGeometryChanged);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Zoom controls (Q/E) — work regardless of palette state
            if (evt.keyCode == KeyCode.Q)
            {
                ZoomSprite(-ZoomStep);
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode == KeyCode.E)
            {
                ZoomSprite(ZoomStep);
                evt.StopPropagation();
                return;
            }

            // Shift + WASD/Arrows = pan sprite atlas
            if (evt.shiftKey)
            {
                switch (evt.keyCode)
                {
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                        PanSprite(0, PanStep);
                        evt.StopPropagation();
                        return;
                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        PanSprite(0, -PanStep);
                        evt.StopPropagation();
                        return;
                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        PanSprite(-PanStep, 0);
                        evt.StopPropagation();
                        return;
                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        PanSprite(PanStep, 0);
                        evt.StopPropagation();
                        return;
                }
            }

            // Palette navigation (no shift)
            if (_colorPallet.Count == 0) return;

            switch (evt.keyCode)
            {
                case KeyCode.W:
                case KeyCode.UpArrow:
                    MoveSelection(0, -1);
                    evt.StopPropagation();
                    break;
                case KeyCode.S:
                case KeyCode.DownArrow:
                    MoveSelection(0, 1);
                    evt.StopPropagation();
                    break;
                case KeyCode.A:
                case KeyCode.LeftArrow:
                    MoveSelection(-1, 0);
                    evt.StopPropagation();
                    break;
                case KeyCode.D:
                case KeyCode.RightArrow:
                    MoveSelection(1, 0);
                    evt.StopPropagation();
                    break;
            }
        }

        private void MoveSelection(int deltaX, int deltaY)
        {
            if (_colorPallet.Count == 0) return;

            int maxColumn = _colorPallet[0].Count - 1;
            int maxRow = _colorPallet.Count - 1;

            // Clamp values to valid range
            _selectedColumn = Mathf.Clamp(_selectedColumn + deltaX, 0, maxColumn);
            _selectedRow = Mathf.Clamp(_selectedRow + deltaY, 0, maxRow);

            UpdateSelectionDisplay();
        }

        // ── Pan & Zoom ───────────────────────────────────────────────────────

        private void PanSprite(float dx, float dy)
        {
            if (_spriteAtlas == null) return;

            float spriteW = _spriteAtlas.width;
            float spriteH = _spriteAtlas.height;

            // Max pan: sprite edge can reach the center of the camera view (3x extended)
            float maxPanX = Mathf.Max(0, spriteW * _zoomLevel * 3f);
            float maxPanY = Mathf.Max(0, spriteH * _zoomLevel * 3f);

            _panOffset.x = Mathf.Clamp(_panOffset.x + dx, -maxPanX, maxPanX);
            _panOffset.y = Mathf.Clamp(_panOffset.y + dy, -maxPanY, maxPanY);

            ApplyPanZoom();
        }

        private void ZoomSprite(float delta)
        {
            if (_spriteAtlas == null) return;

            _zoomLevel = Mathf.Clamp(_zoomLevel + delta, ZoomMin, ZoomMax);

            // Re-clamp pan so sprite stays partially visible (3x extended)
            float spriteW = _spriteAtlas.width;
            float spriteH = _spriteAtlas.height;
            float maxPanX = Mathf.Max(0, spriteW * _zoomLevel * 1.5f);
            float maxPanY = Mathf.Max(0, spriteH * _zoomLevel * 1.5f);
            _panOffset.x = Mathf.Clamp(_panOffset.x, -maxPanX, maxPanX);
            _panOffset.y = Mathf.Clamp(_panOffset.y, -maxPanY, maxPanY);

            ApplyPanZoom();
        }

        private void ApplyPanZoom()
        {
            if (_spriteObject != null)
            {
                // Post-bake: move and scale the sprite GameObject, camera stays fixed
                _spriteObject.transform.position = new Vector3(_panOffset.x, _panOffset.y, 0);
                _spriteObject.transform.localScale = Vector3.one * _zoomLevel;
            }
            else if (_previewContainer != null && _previewContainer.childCount > 0)
            {
                // Pre-bake: transform the Image element
                var image = _previewContainer.Children().FirstOrDefault() as Image;
                if (image != null)
                {
                    image.style.translate = new Translate(
                        new Length(_panOffset.x, LengthUnit.Pixel),
                        new Length(-_panOffset.y, LengthUnit.Pixel));
                    image.style.scale = new Scale(new Vector3(_zoomLevel, _zoomLevel, 1f));
                }
            }
        }

        private void UpdateSelectionDisplay()
        {
            if (_colorPallet.Count == 0) return;

            // Validate selection is within bounds
            if (_selectedRow >= _colorPallet.Count)
                _selectedRow = _colorPallet.Count - 1;
            if (_selectedColumn >= _colorPallet[_selectedRow].Count)
                _selectedColumn = _colorPallet[_selectedRow].Count - 1;

            // Update position label
            if (_positionLabel != null)
            {
                string rowType = _selectedRow == 0 ? " (Base - Readonly)" : "";
                _positionLabel.text = $"Position: ({_selectedColumn}, {_selectedRow}){rowType}";
            }

            // Get selected color
            Color selectedColor = _colorPallet[_selectedRow][_selectedColumn];

            // Update color preview
            if (_selectedColorPreview != null)
                _selectedColorPreview.style.backgroundColor = new StyleColor(selectedColor);

            // Update RGBA inputs (0-255 range)
            if (_rInput != null) _rInput.value = Mathf.RoundToInt(selectedColor.r * 255);
            if (_gInput != null) _gInput.value = Mathf.RoundToInt(selectedColor.g * 255);
            if (_bInput != null) _bInput.value = Mathf.RoundToInt(selectedColor.b * 255);
            if (_aInput != null) _aInput.value = Mathf.RoundToInt(selectedColor.a * 255);

            // Disable inputs if row 0 (readonly)
            bool isReadonly = _selectedRow == 0;
            if (_rInput != null) _rInput.SetEnabled(!isReadonly);
            if (_gInput != null) _gInput.SetEnabled(!isReadonly);
            if (_bInput != null) _bInput.SetEnabled(!isReadonly);
            if (_aInput != null) _aInput.SetEnabled(!isReadonly);

            // Redraw color pallet with selection highlight
            DrawColorPallet();
        }

        private void OnColorInputChanged()
        {
            if (_colorPallet.Count == 0) return;

            // Validate selection is within bounds
            if (_selectedRow >= _colorPallet.Count || _selectedColumn >= _colorPallet[_selectedRow].Count)
                return;

            // Row 0 is readonly (base colors)
            if (_selectedRow == 0)
                return;

            // Get RGBA values from IntegerFields
            int r = Mathf.Clamp(_rInput.value, 0, 255);
            int g = Mathf.Clamp(_gInput.value, 0, 255);
            int b = Mathf.Clamp(_bInput.value, 0, 255);
            int a = Mathf.Clamp(_aInput.value, 0, 255);

            Color newColor = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

            // Update the color in the pallet
            _colorPallet[_selectedRow][_selectedColumn] = newColor;

            // Update preview
            if (_selectedColorPreview != null)
                _selectedColorPreview.style.backgroundColor = new StyleColor(newColor);

            // Redraw
            DrawColorPallet();
        }

        private void OnInterpolateClicked(ClickEvent evt)
        {
            if (_colorPallet.Count == 0) return;

            int column = _selectedColumn;
            int rowCount = _colorPallet.Count;

            // Need at least 2 rows (base + 1 shadow level) to interpolate
            if (rowCount < 2) return;

            // Validate column is within bounds for all rows
            for (int i = 0; i < rowCount; i++)
            {
                if (column >= _colorPallet[i].Count)
                    return;
            }

            // Row 0 is readonly base color, interpolate from base to last row
            Color baseColor = _colorPallet[0][column];
            Color lastColor = _colorPallet[rowCount - 1][column];

            // Interpolate all shadow rows (row 1 through last) from base to last
            for (int i = 1; i < rowCount; i++)
            {
                float t = (float)i / (rowCount - 1);
                _colorPallet[i][column] = Color.Lerp(baseColor, lastColor, t);
            }

            // Redraw
            DrawColorPallet();
            UpdateSelectionDisplay();
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
            cancelButton.style.marginLeft = 10;
            buttonRow.Add(cancelButton);

            content.Add(buttonRow);
            dialog.Add(content);

            root.Add(dialog);
        }
#endif

        private void OnShadowDegreeToggleChanged(ChangeEvent<bool> evt)
        {
            _shadowDegreeEnabled = evt.newValue;
            if (_shadowDegreeInput != null)
                _shadowDegreeInput.SetEnabled(_shadowDegreeEnabled);

            if (_shadowDegreeEnabled && _spriteAtlas != null)
            {
                if (_shadowDegreeInput.value >= 2)
                {
                    _shadowDegree = _shadowDegreeInput.value;
                    GenerateColorPallet();
                }
            }
        }

        private void OnShadowDegreeInputChanged(ChangeEvent<int> evt)
        {
            if (!_shadowDegreeEnabled || _spriteAtlas == null) return;

            if (evt.newValue >= 2)
            {
                _shadowDegree = evt.newValue;
                GenerateColorPallet();
            }
            else if (_infoLabel != null)
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

            // Reset pan/zoom for new sprite
            _panOffset = Vector2.zero;
            _zoomLevel = 1f;

            // Hide waiting label, show preview
            if (_waitingLabel != null)
                _waitingLabel.style.display = DisplayStyle.None;

            // Create preview image
            if (_previewContainer != null)
            {
                _previewContainer.Clear();
                var previewImage = new Image();
                previewImage.image = _spriteAtlas;
                previewImage.scaleMode = ScaleMode.ScaleToFit;
                _previewContainer.Add(previewImage);
            }

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

            // Sort by HSV
            _uniqueColors = temp
                .OrderBy(x => x.h)
                .ThenBy(x => x.s)
                .ThenBy(x => x.v)
                .ThenBy(x => x.color.a)
                .Select(x => x.color)
                .ToList();

            // Generate color pallet
            _colorPallet = new List<List<Color>>();

            // Add base level (level 0)
            _colorPallet.Add(new List<Color>(_uniqueColors));

            // Add shadow levels
            int shadowLevels = _shadowDegreeEnabled ? _shadowDegree : 1;
            for (int i = 1; i < shadowLevels; i++)
            {
                var level = new List<Color>();
                foreach (var color in _uniqueColors)
                {
                    float factor = 1f - (float)i / shadowLevels;
                    level.Add(new Color(color.r * factor, color.g * factor, color.b * factor, color.a));
                }
                _colorPallet.Add(level);
            }

            // Reset selection
            _selectedColumn = 0;
            _selectedRow = 0;

            // Update info label
            if (_infoLabel != null)
                _infoLabel.text = $"Colors: {_uniqueColors.Count} | Levels: {_colorPallet.Count}";

            // Draw color pallet
            DrawColorPallet();
            UpdateSelectionDisplay();
        }

        private List<Color> GetUniqueColors(Texture2D texture)
        {
            HashSet<Color> uniqueColors = new HashSet<Color>();
            Color[] pixels = texture.GetPixels();

            foreach (var pixel in pixels)
            {
                if (pixel.a > 0.01f)
                {
                    uniqueColors.Add(pixel);
                }
            }

            return uniqueColors.ToList();
        }

        private void DrawColorPallet()
        {
            if (_palletContainer == null) return;

            _palletContainer.Clear();

            if (_colorPallet.Count == 0) return;

            int width = _colorPallet[0].Count;
            int height = _colorPallet.Count;

            // Sliding window: show up to 30 columns centered on selection
            int windowSize = 30;
            int halfWindow = windowSize / 2;

            int startColumn = Mathf.Max(0, _selectedColumn - halfWindow);
            int endColumn = Mathf.Min(width - 1, startColumn + windowSize - 1);

            // Adjust start if we're near the end
            if (endColumn - startColumn < windowSize - 1)
            {
                startColumn = Mathf.Max(0, endColumn - windowSize + 1);
            }

            for (int y = 0; y < height; y++)
            {
                // Add row label for row 0
                if (y == 0)
                {
                    var rowLabel = new Label("Base (Readonly):");
                    rowLabel.style.fontSize = 10;
                    rowLabel.style.color = new StyleColor(Color.gray);
                    rowLabel.style.marginBottom = 2;
                    _palletContainer.Add(rowLabel);
                }

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;

                for (int x = startColumn; x <= endColumn; x++)
                {
                    if (x < _colorPallet[y].Count)
                    {
                        var cell = new VisualElement();
                        cell.style.width = 12;
                        cell.style.height = 12;
                        cell.style.backgroundColor = new StyleColor(_colorPallet[y][x]);

                        // Dim row 0 to indicate readonly
                        if (y == 0)
                        {
                            cell.style.opacity = 0.7f;
                        }

                        // Highlight selected cell
                        if (x == _selectedColumn && y == _selectedRow)
                        {
                            cell.style.borderTopWidth = 2;
                            cell.style.borderBottomWidth = 2;
                            cell.style.borderLeftWidth = 2;
                            cell.style.borderRightWidth = 2;
                            cell.style.borderTopColor = new StyleColor(Color.white);
                            cell.style.borderBottomColor = new StyleColor(Color.white);
                            cell.style.borderLeftColor = new StyleColor(Color.white);
                            cell.style.borderRightColor = new StyleColor(Color.white);
                        }

                        // Capture x,y for callback
                        int capturedX = x;
                        int capturedY = y;
                        cell.RegisterCallback<ClickEvent>(e =>
                        {
                            _selectedColumn = capturedX;
                            _selectedRow = capturedY;
                            UpdateSelectionDisplay();
                        });

                        row.Add(cell);
                    }
                }

                _palletContainer.Add(row);

                // Add separator after row 0
                if (y == 0)
                {
                    var separator = new VisualElement();
                    separator.style.height = 2;
                    separator.style.backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.5f));
                    separator.style.marginTop = 3;
                    separator.style.marginBottom = 3;
                    _palletContainer.Add(separator);
                }
            }

            // Show window info
            var windowInfo = new Label($"Columns {startColumn}-{endColumn} of {width} | WASD/Arrows to navigate");
            windowInfo.style.fontSize = 10;
            windowInfo.style.color = new StyleColor(Color.gray);
            windowInfo.style.marginTop = 5;
            _palletContainer.Add(windowInfo);
        }

        // ── Color Picker ──────────────────────────────────────────────────────

        private void OnColorPreviewClicked(ClickEvent evt)
        {
            if (_colorPallet.Count == 0) return;
            if (_selectedRow == 0) return; // readonly base row
            if (_selectedRow >= _colorPallet.Count || _selectedColumn >= _colorPallet[_selectedRow].Count) return;

            OpenColorPicker(_colorPallet[_selectedRow][_selectedColumn]);
        }

        private void OpenColorPicker(Color initialColor)
        {
            CloseColorPicker();

            var root = _uiDocument.rootVisualElement;
            RemoveStaleColorPickerOverlays(root);

            // Convert initial color to HSV
            Color.RGBToHSV(initialColor, out _pickerH, out _pickerS, out _pickerV);
            _pickerA = initialColor.a;

            // Overlay
            _pickerOverlay = new VisualElement();
            _pickerOverlay.name = "color-picker-overlay";
            _pickerOverlay.AddToClassList("color-picker-overlay");
            _pickerOverlay.RegisterCallback<ClickEvent>(evt =>
            {
                // Close if clicking the overlay backdrop itself
                if (evt.target == _pickerOverlay)
                    CloseColorPicker();
            });

            // Panel
            var panel = new VisualElement();
            panel.name = "color-picker-panel";
            panel.AddToClassList("color-picker-panel");
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // Title
            var title = new Label("Color Picker");
            title.AddToClassList("picker-title");
            panel.Add(title);

            // Content row
            var content = new VisualElement();
            content.AddToClassList("picker-content");

            // Left: SV picker + hue + alpha
            var left = new VisualElement();
            left.AddToClassList("picker-left");

            // SV Picker
            _svPicker = new VisualElement();
            _svPicker.AddToClassList("sv-picker");
            _svTexture = new Texture2D(180, 180, TextureFormat.RGBA32, false);
            _svTexture.filterMode = FilterMode.Bilinear;
            _svPicker.style.backgroundImage = new StyleBackground(_svTexture);
            RegisterPickerPointerEvents(_svPicker, OnSVDrag);
            left.Add(_svPicker);

            // Hue Slider
            _hueSlider = new VisualElement();
            _hueSlider.AddToClassList("hue-slider");
            _hueTexture = new Texture2D(180, 20, TextureFormat.RGBA32, false);
            _hueTexture.filterMode = FilterMode.Bilinear;
            _hueSlider.style.backgroundImage = new StyleBackground(_hueTexture);
            RegisterPickerPointerEvents(_hueSlider, OnHueDrag);
            left.Add(_hueSlider);

            // Alpha Slider
            _alphaSlider = new VisualElement();
            _alphaSlider.AddToClassList("alpha-slider");
            _alphaTexture = new Texture2D(180, 20, TextureFormat.RGBA32, false);
            _alphaTexture.filterMode = FilterMode.Bilinear;
            _alphaSlider.style.backgroundImage = new StyleBackground(_alphaTexture);
            RegisterPickerPointerEvents(_alphaSlider, OnAlphaDrag);
            left.Add(_alphaSlider);

            content.Add(left);

            // Right: preview + RGBA inputs
            var right = new VisualElement();
            right.AddToClassList("picker-right");

            _pickerPreview = new VisualElement();
            _pickerPreview.AddToClassList("picker-preview-box");
            right.Add(_pickerPreview);

            var rgbaInputs = new VisualElement();
            rgbaInputs.AddToClassList("picker-rgba-inputs");

            _pickerRInput = CreatePickerInput(rgbaInputs, "R:");
            _pickerGInput = CreatePickerInput(rgbaInputs, "G:");
            _pickerBInput = CreatePickerInput(rgbaInputs, "B:");
            _pickerAInput = CreatePickerInput(rgbaInputs, "A:");

            right.Add(rgbaInputs);
            content.Add(right);

            panel.Add(content);

            // Buttons
            var buttons = new VisualElement();
            buttons.AddToClassList("picker-buttons");

            var okButton = new Button(() =>
            {
                ApplyPickerColor();
                CloseColorPicker();
            });
            okButton.text = "OK";
            okButton.AddToClassList("picker-ok");
            buttons.Add(okButton);

            var cancelButton = new Button(() => CloseColorPicker());
            cancelButton.text = "Cancel";
            cancelButton.AddToClassList("picker-cancel");
            buttons.Add(cancelButton);

            panel.Add(buttons);
            _pickerOverlay.Add(panel);
            root.Add(_pickerOverlay);

            UpdatePickerDisplay();
        }

        private IntegerField CreatePickerInput(VisualElement container, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("picker-rgba-row");

            var lbl = new Label(label);
            lbl.AddToClassList("picker-rgba-label");
            row.Add(lbl);

            var input = new IntegerField();
            input.AddToClassList("picker-rgba-input");
            input.RegisterCallback<ChangeEvent<int>>(OnPickerInputChanged);
            row.Add(input);

            container.Add(row);
            return input;
        }

        private void RegisterPickerPointerEvents(VisualElement element, Action<float, float> onDrag)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                element.CapturePointer(evt.pointerId);
                var localPos = element.WorldToLocal(evt.position);
                onDrag(localPos.x, localPos.y);
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!element.HasPointerCapture(evt.pointerId)) return;
                var localPos = element.WorldToLocal(evt.position);
                onDrag(localPos.x, localPos.y);
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                element.ReleasePointer(evt.pointerId);
            });
        }

        private void OnSVDrag(float localX, float localY)
        {
            float w = _svPicker.resolvedStyle.width;
            float h = _svPicker.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            _pickerS = Mathf.Clamp01(localX / w);
            _pickerV = Mathf.Clamp01(1f - localY / h);

            UpdatePickerDisplay();
        }

        private void OnHueDrag(float localX, float localY)
        {
            float w = _hueSlider.resolvedStyle.width;
            if (w <= 0) return;

            _pickerH = Mathf.Clamp01(localX / w);
            UpdatePickerDisplay();
        }

        private void OnAlphaDrag(float localX, float localY)
        {
            float w = _alphaSlider.resolvedStyle.width;
            if (w <= 0) return;

            _pickerA = Mathf.Clamp01(localX / w);
            UpdatePickerDisplay();
        }

        private void OnPickerInputChanged(ChangeEvent<int> evt)
        {
            if (_updatingPickerInputs) return;

            int r = Mathf.Clamp(_pickerRInput.value, 0, 255);
            int g = Mathf.Clamp(_pickerGInput.value, 0, 255);
            int b = Mathf.Clamp(_pickerBInput.value, 0, 255);
            int a = Mathf.Clamp(_pickerAInput.value, 0, 255);

            Color c = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            Color.RGBToHSV(c, out _pickerH, out _pickerS, out _pickerV);
            _pickerA = c.a;

            UpdatePickerDisplay();
        }

        private void UpdatePickerDisplay()
        {
            Color currentColor = Color.HSVToRGB(_pickerH, _pickerS, _pickerV);
            currentColor.a = _pickerA;

            // Generate SV texture
            for (int y = 0; y < 180; y++)
            {
                for (int x = 0; x < 180; x++)
                {
                    float s = x / 179f;
                    // Texture Y grows upward, while local pointer Y grows downward.
                    // Draw the texture so the visible top edge corresponds to V = 1.
                    float v = y / 179f;
                    Color pixel = Color.HSVToRGB(_pickerH, s, v);
                    pixel.a = 1f;
                    _svTexture.SetPixel(x, y, pixel);
                }
            }
            _svTexture.Apply();

            // Generate hue texture
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 180; x++)
                {
                    float h = x / 179f;
                    Color pixel = Color.HSVToRGB(h, 1f, 1f);
                    pixel.a = 1f;
                    _hueTexture.SetPixel(x, y, pixel);
                }
            }
            _hueTexture.Apply();

            // Generate alpha texture
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 180; x++)
                {
                    float a = x / 179f;
                    Color pixel = currentColor;
                    pixel.a = a;
                    _alphaTexture.SetPixel(x, y, pixel);
                }
            }
            _alphaTexture.Apply();

            // Update preview
            if (_pickerPreview != null)
                _pickerPreview.style.backgroundColor = new StyleColor(currentColor);

            // Update RGBA inputs
            _updatingPickerInputs = true;
            if (_pickerRInput != null) _pickerRInput.value = Mathf.RoundToInt(currentColor.r * 255);
            if (_pickerGInput != null) _pickerGInput.value = Mathf.RoundToInt(currentColor.g * 255);
            if (_pickerBInput != null) _pickerBInput.value = Mathf.RoundToInt(currentColor.b * 255);
            if (_pickerAInput != null) _pickerAInput.value = Mathf.RoundToInt(currentColor.a * 255);
            _updatingPickerInputs = false;
        }

        private void ApplyPickerColor()
        {
            if (_colorPallet.Count == 0) return;
            if (_selectedRow == 0) return;
            if (_selectedRow >= _colorPallet.Count || _selectedColumn >= _colorPallet[_selectedRow].Count) return;

            Color pickedColor = Color.HSVToRGB(_pickerH, _pickerS, _pickerV);
            pickedColor.a = _pickerA;

            _colorPallet[_selectedRow][_selectedColumn] = pickedColor;

            // Update main RGBA inputs
            if (_rInput != null) _rInput.value = Mathf.RoundToInt(pickedColor.r * 255);
            if (_gInput != null) _gInput.value = Mathf.RoundToInt(pickedColor.g * 255);
            if (_bInput != null) _bInput.value = Mathf.RoundToInt(pickedColor.b * 255);
            if (_aInput != null) _aInput.value = Mathf.RoundToInt(pickedColor.a * 255);

            // Update main preview
            if (_selectedColorPreview != null)
                _selectedColorPreview.style.backgroundColor = new StyleColor(pickedColor);

            DrawColorPallet();
        }

        private void CloseColorPicker()
        {
            var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;

            if (_pickerOverlay != null)
            {
                _pickerOverlay.style.display = DisplayStyle.None;
                _pickerOverlay.RemoveFromHierarchy();
                _pickerOverlay = null;
            }

            if (root != null)
                RemoveStaleColorPickerOverlays(root);

            if (_svTexture != null) { Destroy(_svTexture); _svTexture = null; }
            if (_hueTexture != null) { Destroy(_hueTexture); _hueTexture = null; }
            if (_alphaTexture != null) { Destroy(_alphaTexture); _alphaTexture = null; }

            _svPicker = null;
            _hueSlider = null;
            _alphaSlider = null;
            _pickerPreview = null;
            _pickerRInput = null;
            _pickerGInput = null;
            _pickerBInput = null;
            _pickerAInput = null;
        }

        private void RemoveStaleColorPickerOverlays(VisualElement root)
        {
            foreach (var overlay in root.Query<VisualElement>("color-picker-overlay").ToList())
                overlay.RemoveFromHierarchy();
        }

        // ── Bake & Scene ──────────────────────────────────────────────────────

        private void OnBakeClicked(ClickEvent evt)
        {
            if (_colorPallet.Count == 0 || _spriteAtlas == null) return;

            BakeLut();
            CreateSceneObjects();

            // Restore keyboard focus to root after baking
            var root = _uiDocument.rootVisualElement;
            root.Focus();
        }

        private void BakeLut()
        {
            int shadowDegree = _colorPallet.Count;
            int colorCount = _colorPallet[0].Count;
            int lutSize = colorCount <= 16 ? 16 : colorCount <= 32 ? 32 : 64;

            int width = lutSize * lutSize;
            int height = lutSize;

            // Build identity LUT
            var identityLut = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = x % lutSize / (lutSize - 1f);
                    float g = y / (lutSize - 1f);
                    float b = (x / lutSize) / (lutSize - 1f);
                    identityLut[y * width + x] = new Color(r, g, b, 1f);
                }
            }

            // Index: map each LUT pixel to nearest palette base color
            var indexedColors = new Color[width * height];
            for (int i = 0; i < identityLut.Length; i++)
            {
                Color lutColor = identityLut[i];
                float bestDist = float.MaxValue;
                int bestIdx = 0;

                for (int c = 0; c < colorCount; c++)
                {
                    Color paletteColor = _colorPallet[0][c];
                    float dr = (lutColor.r - paletteColor.r) * 0.299f;
                    float dg = (lutColor.g - paletteColor.g) * 0.587f;
                    float db = (lutColor.b - paletteColor.b) * 0.114f;
                    // Weighted Euclidean distance (rec601) — matches LutGenerator
                    float d = Mathf.Sqrt(dr * dr + dg * dg + db * db);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestIdx = c;
                    }
                }

                indexedColors[i] = _colorPallet[0][bestIdx];
            }

            // Find shapes: for each palette color, collect LUT positions
            var shapes = new List<List<int>>();
            for (int c = 0; c < colorCount; c++)
            {
                var shape = new List<int>();
                Color paletteColor = _colorPallet[0][c];
                for (int i = 0; i < indexedColors.Length; i++)
                {
                    if (indexedColors[i] == paletteColor)
                        shape.Add(i);
                }
                shapes.Add(shape);
            }

            // Generate LUT texture with shadow grades
            // Row order: deepest shadow at bottom, lightest at top (matches LutGenerator)
            int texHeight = height * shadowDegree;
            var pixels = new Color[width * texHeight];

            for (int gradeRow = 0; gradeRow < shadowDegree; gradeRow++)
            {
                // Reverse mapping: gradeRow 0 -> bottom of texture, gradeRow N -> top
                int yOffset = (shadowDegree - 1 - gradeRow) * height;
                for (int c = 0; c < colorCount; c++)
                {
                    Color gradeColor = _colorPallet[gradeRow][c];
                    foreach (int pos in shapes[c])
                    {
                        int srcY = pos / width;
                        int srcX = pos % width;
                        int destY = yOffset + srcY;
                        pixels[destY * width + srcX] = gradeColor;
                    }
                }
            }

            // Create texture
            if (_lutTexture != null) Destroy(_lutTexture);
            _lutTexture = new Texture2D(width, texHeight, TextureFormat.RGBA32, false, true);

            _lutTexture.filterMode = FilterMode.Point;
            _lutTexture.SetPixels(pixels);
            _lutTexture.Apply();

            // Create material
            var shader = Shader.Find("Shader Graphs/LutLight");
            if (shader == null)
            {
                Debug.LogError("LutLight shader not found!");
                return;
            }

            if (_lutMaterial != null) Destroy(_lutMaterial);
            _lutMaterial = new Material(shader);
            _lutMaterial.SetTexture("_Lut", _lutTexture);
            _lutMaterial.SetFloat("_Grades", shadowDegree);
            _lutMaterial.SetInt("_LUT_SIZE", lutSize);
            _lutMaterial.SetFloat("_Light_Impact", 0f);
            _lutMaterial.SetFloat("_Texel_Size", 1f);

            // Clear all keywords first, then set the correct ones
            _lutMaterial.shaderKeywords = new string[0];
            _lutMaterial.EnableKeyword(lutSize switch
            {
                16  => "_LUT_SIZE_X16",
                32  => "_LUT_SIZE_X32",
                64  => "_LUT_SIZE_X64",
                _   => "_LUT_SIZE_X16"
            });
            _lutMaterial.EnableKeyword("_GRADING_CHROMA");
            _lutMaterial.EnableKeyword("_PIXELATED");
            _lutMaterial.SetFloat("_Texel_Size", 1f);

            if (_infoLabel != null)
                _infoLabel.text = $"Baked: {width}x{texHeight} LUT, {colorCount} colors, {shadowDegree} grades";

            // Debug: verify material setup
            Debug.Log($"LutLight Bake: shader={shader.name}, lut={_lutTexture.width}x{_lutTexture.height}, " +
                      $"grades={shadowDegree}, lutSize={lutSize}, keywords={string.Join(", ", _lutMaterial.shaderKeywords)}");
            Debug.Log($"  _Lut property: {_lutMaterial.GetTexture("_Lut") != null}, " +
                      $"_Grades={_lutMaterial.GetFloat("_Grades")}, " +
                      $"_LUT_SIZE={_lutMaterial.GetInt("_LUT_SIZE")}, " +
                      $"_Light_Impact={_lutMaterial.GetFloat("_Light_Impact")}");
            Debug.Log($"  LUT first pixel: {_lutTexture.GetPixel(0, 0)}, " +
                      $"LUT center pixel: {_lutTexture.GetPixel(width / 2, texHeight / 2)}");
            Debug.Log($"  Material shader valid: {shader.isSupported}, " +
                      $"pass count: {_lutMaterial.passCount}");

            // Verify LUT has different colors per grade
            Color bottomColor = _lutTexture.GetPixel(0, 0);            // shadow grade
            Color topColor = _lutTexture.GetPixel(0, texHeight - 1);   // base grade
            Debug.Log($"  LUT bottom (shadow): {bottomColor}, LUT top (base): {topColor}, different: {bottomColor != topColor}");
        }

        private void CreateSceneObjects()
        {
            DestroySceneObjects();

            var tex = _spriteAtlas;
            float pixelsPerUnit = 1f; // 1 pixel = 1 unit for pixel-perfect
            float spriteWorldWidth = tex.width / pixelsPerUnit;
            float spriteWorldHeight = tex.height / pixelsPerUnit;
            float halfMaxSize = Mathf.Max(spriteWorldWidth, spriteWorldHeight) * 0.5f;

            // Create sprite object
            _spriteObject = new GameObject("BakedSprite");
            _spriteObject.transform.position = Vector3.zero;

            var sr = _spriteObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _lutMaterial;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sr.sprite.texture.filterMode = FilterMode.Point;
            sr.sprite.texture.wrapMode = TextureWrapMode.Clamp;

            // Create point light
            var lightObj = new GameObject("PointLight2D");
            lightObj.transform.position = Vector3.zero;

            _pointLight = lightObj.AddComponent<Light2D>();
            _pointLight.lightType = Light2D.LightType.Point;
            _pointLight.color = Color.white;
            _pointLight.intensity = _lightIntensitySlider != null ? _lightIntensitySlider.value : 1f;
            _pointLight.intensity = 1f;
            float radius = _lightIntensitySlider != null ? _lightIntensitySlider.value : 25f;
            _pointLight.pointLightOuterRadius = radius;
            _pointLight.pointLightInnerRadius = radius * 0.25f;
            _pointLight.blendStyleIndex = 0; // Multiply blend

            // Setup Main Camera — fixed position, shows full sprite at zoom=1
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 0, -10);
                cam.orthographic = true;
                cam.orthographicSize = halfMaxSize;
                cam.gateFit = Camera.GateFitMode.Overscan;
            }

            // Apply pan offset and zoom to sprite
            _spriteObject.transform.position = new Vector3(_panOffset.x, _panOffset.y, 0);
            _spriteObject.transform.localScale = Vector3.one * _zoomLevel;

            // Make preview container transparent so scene shows through
            if (_previewContainer != null)
            {
                _previewContainer.Clear();
                _previewContainer.style.backgroundColor = new StyleColor(Color.clear);
            }

            // Register pointer events for light dragging
            if (_previewContainer != null)
            {
                _previewContainer.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown);
                _previewContainer.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
                _previewContainer.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);
            }

            UpdatePreviewCameraViewport();
        }

        private void DestroySceneObjects()
        {
            if (_spriteObject != null) { Destroy(_spriteObject); _spriteObject = null; }
            if (_pointLight != null) { Destroy(_pointLight.gameObject); _pointLight = null; }

            var cam = Camera.main;
            if (cam != null)
                cam.pixelRect = new Rect(0, 0, Screen.width, Screen.height);

            if (_previewContainer != null)
            {
                _previewContainer.UnregisterCallback<PointerDownEvent>(OnPreviewPointerDown);
                _previewContainer.UnregisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
                _previewContainer.UnregisterCallback<PointerUpEvent>(OnPreviewPointerUp);
            }
        }

        private void OnPreviewPointerDown(PointerDownEvent evt)
        {
            if (_pointLight == null) return;

            _isDraggingLight = true;
            _previewContainer.CapturePointer(evt.pointerId);
            MoveLightToPointer(evt.position);

            // Keep focus on root so keyboard events (Shift+WASD, Q/E) still work
            _uiDocument.rootVisualElement.Focus();
        }

        private void OnPreviewPointerMove(PointerMoveEvent evt)
        {
            if (!_isDraggingLight || _pointLight == null) return;
            if (!_previewContainer.HasPointerCapture(evt.pointerId)) return;

            MoveLightToPointer(evt.position);
        }

        private void OnPreviewPointerUp(PointerUpEvent evt)
        {
            _isDraggingLight = false;
            if (_previewContainer.HasPointerCapture(evt.pointerId))
                _previewContainer.ReleasePointer(evt.pointerId);
        }

        private void OnPreviewGeometryChanged(GeometryChangedEvent evt)
        {
            UpdatePreviewCameraViewport();
        }

        private float GetPanelPixelsPerPoint()
        {
            float pixelsPerPoint = _previewContainer?.panel?.scaledPixelsPerPoint ?? 1f;
            return pixelsPerPoint > 0f ? pixelsPerPoint : 1f;
        }

        private void UpdatePreviewCameraViewport()
        {
            var cam = Camera.main;
            if (cam == null || _previewContainer == null) return;

            Rect previewBounds = _previewContainer.worldBound;
            if (previewBounds.width <= 0f || previewBounds.height <= 0f) return;

            float pixelsPerPoint = GetPanelPixelsPerPoint();
            cam.pixelRect = new Rect(
                previewBounds.x * pixelsPerPoint,
                Screen.height - (previewBounds.y + previewBounds.height) * pixelsPerPoint,
                previewBounds.width * pixelsPerPoint,
                previewBounds.height * pixelsPerPoint
            );
        }

        private void MoveLightToPointer(Vector2 panelPos)
        {
            var cam = Camera.main;
            if (cam == null || _pointLight == null) return;
            if (_previewContainer == null) return;

            Vector2 localPos = _previewContainer.WorldToLocal(panelPos);
            Rect contentRect = _previewContainer.contentRect;
            if (contentRect.width <= 0f || contentRect.height <= 0f) return;

            float viewportX = Mathf.Clamp01(localPos.x / contentRect.width);
            float viewportY = Mathf.Clamp01(1f - (localPos.y / contentRect.height));

            Vector3 worldPos = cam.ViewportToWorldPoint(new Vector3(viewportX, viewportY, -cam.transform.position.z));
            _pointLight.transform.position = new Vector3(worldPos.x, worldPos.y, 0);
        }

        private void OnLightIntensityChanged(ChangeEvent<float> evt)
        {
            if (_lightIntensityValue != null)
                _lightIntensityValue.text = Mathf.RoundToInt(evt.newValue).ToString();

            if (_pointLight != null)
            {
                _pointLight.pointLightOuterRadius = evt.newValue;
                _pointLight.pointLightInnerRadius = evt.newValue * 0.25f;
            }
        }

        // ── Download & Restore ────────────────────────────────────────────────

        private void OnDownloadClicked(ClickEvent evt)
        {
            if (_colorPallet.Count == 0) return;

#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanel("Save Color Pallet", "", "color_pallet", "png");
            if (!string.IsNullOrEmpty(path))
            {
                DownloadPalette(path);
            }
#else
            ShowSaveFileDialog();
#endif
        }

#if !UNITY_EDITOR
        private void ShowSaveFileDialog()
        {
            var root = _uiDocument.rootVisualElement;

            var dialog = new VisualElement();
            dialog.name = "save-file-dialog";
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

            var titleLabel = new Label("Enter save path for color pallet (.png):");
            titleLabel.style.color = Color.white;
            titleLabel.style.fontSize = 14;
            titleLabel.style.marginBottom = 10;
            content.Add(titleLabel);

            var input = new TextField();
            input.value = "color_pallet.png";
            content.Add(input);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 15;
            var okButton = new Button(() =>
            {
                string path = input.value;
                root.Remove(dialog);
                if (!string.IsNullOrEmpty(path))
                {
                    DownloadPalette(path);
                }
            });
            okButton.text = "OK";
            okButton.style.width = 80;
            buttonRow.Add(okButton);

            var cancelButton = new Button(() => root.Remove(dialog));
            cancelButton.text = "Cancel";
            cancelButton.style.width = 80;
            cancelButton.style.marginLeft = 10;
            buttonRow.Add(cancelButton);

            content.Add(buttonRow);
            dialog.Add(content);
            root.Add(dialog);
        }
#endif

        private void DownloadPalette(string path)
        {
            if (_colorPallet.Count == 0) return;

            int width = _colorPallet[0].Count;
            int height = _colorPallet.Count;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Row 0 in texture = base colors (top), row N = deepest shadow (bottom)
                    int palletRow = height - 1 - y;
                    texture.SetPixel(x, y, _colorPallet[palletRow][x]);
                }
            }

            texture.Apply();
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            Destroy(texture);

            if (_infoLabel != null)
                _infoLabel.text = $"Saved: {path}";
        }

        private void OnRestoreClicked(ClickEvent evt)
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Open Color Pallet", "", "png");
            if (!string.IsNullOrEmpty(path))
            {
                RestorePalette(path);
            }
#else
            ShowOpenFileDialog();
#endif
        }

#if !UNITY_EDITOR
        private void ShowOpenFileDialog()
        {
            var root = _uiDocument.rootVisualElement;

            var dialog = new VisualElement();
            dialog.name = "open-file-dialog";
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

            var titleLabel = new Label("Enter path to color pallet (.png):");
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
            var okButton = new Button(() =>
            {
                string path = input.value;
                root.Remove(dialog);
                if (!string.IsNullOrEmpty(path))
                {
                    RestorePalette(path);
                }
            });
            okButton.text = "OK";
            okButton.style.width = 80;
            buttonRow.Add(okButton);

            var cancelButton = new Button(() => root.Remove(dialog));
            cancelButton.text = "Cancel";
            cancelButton.style.width = 80;
            cancelButton.style.marginLeft = 10;
            buttonRow.Add(cancelButton);

            content.Add(buttonRow);
            dialog.Add(content);
            root.Add(dialog);
        }
#endif

        private void RestorePalette(string path)
        {
            if (!File.Exists(path))
            {
                if (_infoLabel != null)
                    _infoLabel.text = "File not found";
                return;
            }

            byte[] fileData = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(fileData))
            {
                Destroy(texture);
                if (_infoLabel != null)
                    _infoLabel.text = "Failed to load image";
                return;
            }

            LoadPaletteFromPng(texture);
            Destroy(texture);
        }

        private void LoadPaletteFromPng(Texture2D texture)
        {
            int width = texture.width;
            int height = texture.height;

            _colorPallet = new List<List<Color>>();

            for (int y = 0; y < height; y++)
            {
                var row = new List<Color>();
                for (int x = 0; x < width; x++)
                {
                    // Row 0 in texture = base colors (top), row N = deepest shadow (bottom)
                    int palletRow = height - 1 - y;
                    row.Add(texture.GetPixel(x, palletRow));
                }
                _colorPallet.Add(row);
            }

            // Update shadow degree to match loaded palette
            _shadowDegree = height;
            if (_shadowDegreeInput != null)
                _shadowDegreeInput.value = height;

            // Reset selection
            _selectedColumn = 0;
            _selectedRow = 0;

            if (_infoLabel != null)
                _infoLabel.text = $"Restored: {width} colors, {height} levels";

            DrawColorPallet();
            UpdateSelectionDisplay();
        }
    }
}
