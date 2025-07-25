using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Reactive;
using Avalonia.VisualTree;

namespace AvaloniaSearchableComboBox
{
    /// <summary>
    /// A drop-down list control with search functionality.
    /// </summary>
    [TemplatePart("PART_Popup", typeof(Popup), IsRequired = true)]
    [TemplatePart("PART_EditableTextBox", typeof(TextBox), IsRequired = true)]
    [PseudoClasses(pcDropdownOpen, pcPressed)]
    public class SearchableComboBox : SelectingItemsControl
    {
        internal const string pcDropdownOpen = ":dropdownopen";
        internal const string pcPressed = ":pressed";

        /// <summary>
        /// The default value for the <see cref="ItemsControl.ItemsPanel"/> property.
        /// </summary>
        private static readonly FuncTemplate<Panel?> DefaultPanel =
            new(() => new VirtualizingStackPanel());

        /// <summary>
        /// Defines the <see cref="IsDropDownOpen"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsDropDownOpenProperty =
            AvaloniaProperty.Register<SearchableComboBox, bool>(nameof(IsDropDownOpen));

        /// <summary>
        /// Defines the <see cref="MaxDropDownHeight"/> property.
        /// </summary>
        public static readonly StyledProperty<double> MaxDropDownHeightProperty =
            AvaloniaProperty.Register<SearchableComboBox, double>(nameof(MaxDropDownHeight), 200);

        /// <summary>
        /// Defines the <see cref="Text"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<SearchableComboBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="FilterText"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> FilterTextProperty =
            AvaloniaProperty.Register<SearchableComboBox, string?>(nameof(FilterText), defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="PlaceholderText"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> PlaceholderTextProperty =
            AvaloniaProperty.Register<SearchableComboBox, string?>(nameof(PlaceholderText));

        /// <summary>
        /// Defines the <see cref="PlaceholderForeground"/> property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> PlaceholderForegroundProperty =
            AvaloniaProperty.Register<SearchableComboBox, IBrush?>(nameof(PlaceholderForeground));

        /// <summary>
        /// Defines the <see cref="HorizontalContentAlignment"/> property.
        /// </summary>
        public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
            ContentControl.HorizontalContentAlignmentProperty.AddOwner<SearchableComboBox>();

        /// <summary>
        /// Defines the <see cref="VerticalContentAlignment"/> property.
        /// </summary>
        public static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty =
            ContentControl.VerticalContentAlignmentProperty.AddOwner<SearchableComboBox>();

        /// <summary>
        /// Defines the <see cref="FilterFunction"/> property.
        /// </summary>
        public static readonly StyledProperty<Func<object?, string?, bool>?> FilterFunctionProperty =
            AvaloniaProperty.Register<SearchableComboBox, Func<object?, string?, bool>?>(nameof(FilterFunction));

        /// <summary>
        /// Defines the <see cref="AllItems"/> property for storing original items.
        /// </summary>
        public static readonly DirectProperty<SearchableComboBox, IEnumerable?> AllItemsProperty =
            AvaloniaProperty.RegisterDirect<SearchableComboBox, IEnumerable?>(nameof(AllItems), o => o.AllItems, (o, v) => o.AllItems = v);

        private Popup? _popup;
        private TextBox? _editableTextBox;
        private readonly CompositeDisposable _subscriptionsOnOpen = new CompositeDisposable();
        private IEnumerable? _allItems;
        private bool _isUpdatingText;

        /// <summary>
        /// Initializes static members of the <see cref="SearchableComboBox"/> class.
        /// </summary>
        static SearchableComboBox()
        {
            ItemsPanelProperty.OverrideDefaultValue<SearchableComboBox>(DefaultPanel);
            FocusableProperty.OverrideDefaultValue<SearchableComboBox>(true);
            IsTextSearchEnabledProperty.OverrideDefaultValue<SearchableComboBox>(false); // Disable default text search
        }

        /// <summary>
        /// Occurs after the drop-down (popup) list closes.
        /// </summary>
        public event EventHandler? DropDownClosed;

        /// <summary>
        /// Occurs after the drop-down (popup) list opens.
        /// </summary>
        public event EventHandler? DropDownOpened;

        /// <summary>
        /// Gets or sets a value indicating whether the dropdown is currently open.
        /// </summary>
        public bool IsDropDownOpen
        {
            get => GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum height for the dropdown list.
        /// </summary>
        public double MaxDropDownHeight
        {
            get => GetValue(MaxDropDownHeightProperty);
            set => SetValue(MaxDropDownHeightProperty, value);
        }

        /// <summary>
        /// Gets or sets the text content of the control.
        /// </summary>
        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// Gets or sets the filter text for searching items.
        /// </summary>
        public string? FilterText
        {
            get => GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        /// <summary>
        /// Gets or sets the PlaceHolder text.
        /// </summary>
        public string? PlaceholderText
        {
            get => GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        /// <summary>
        /// Gets or sets the Brush that renders the placeholder text.
        /// </summary>
        public IBrush? PlaceholderForeground
        {
            get => GetValue(PlaceholderForegroundProperty);
            set => SetValue(PlaceholderForegroundProperty, value);
        }

        /// <summary>
        /// Gets or sets the horizontal alignment of the content within the control.
        /// </summary>
        public HorizontalAlignment HorizontalContentAlignment
        {
            get => GetValue(HorizontalContentAlignmentProperty);
            set => SetValue(HorizontalContentAlignmentProperty, value);
        }

        /// <summary>
        /// Gets or sets the vertical alignment of the content within the control.
        /// </summary>
        public VerticalAlignment VerticalContentAlignment
        {
            get => GetValue(VerticalContentAlignmentProperty);
            set => SetValue(VerticalContentAlignmentProperty, value);
        }

        /// <summary>
        /// Gets or sets the filter function used to determine which items match the search text.
        /// </summary>
        public Func<object?, string?, bool>? FilterFunction
        {
            get => GetValue(FilterFunctionProperty);
            set => SetValue(FilterFunctionProperty, value);
        }

        /// <summary>
        /// Gets or sets all items before filtering.
        /// </summary>
        public IEnumerable? AllItems
        {
            get => _allItems;
            set => SetAndRaise(AllItemsProperty, ref _allItems, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (AllItems == null && ItemsSource != null)
            {
                AllItems = ItemsSource;
            }
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new SearchableComboBoxItem();
        }

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            return NeedsContainer<SearchableComboBoxItem>(item, out recycleKey);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled)
                return;

            if ((e.Key == Key.F4 && e.KeyModifiers.HasFlag(KeyModifiers.Alt) == false) ||
                ((e.Key == Key.Down || e.Key == Key.Up) && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
            {
                SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
                e.Handled = true;
            }
            else if (IsDropDownOpen && e.Key == Key.Escape)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
                e.Handled = true;
            }
            else if (!IsDropDownOpen && (e.Key == Key.Enter || e.Key == Key.Space))
            {
                SetCurrentValue(IsDropDownOpenProperty, true);
                e.Handled = true;
            }
            else if (IsDropDownOpen && e.Key == Key.Enter)
            {
                SelectFocusedItem();
                SetCurrentValue(IsDropDownOpenProperty, false);
                e.Handled = true;
            }
            else if (IsDropDownOpen && ItemCount > 0 && (e.Key == Key.Down || e.Key == Key.Up))
            {
                var direction = e.Key == Key.Down ? 1 : -1;
                MoveFocus(direction);
                e.Handled = true;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.Source is Visual source && _popup?.IsInsidePopup(source) == true)
            {
                e.Handled = true;
                return;
            }

            // Don't toggle dropdown when clicking on the TextBox
            if (e.Source == _editableTextBox)
            {
                if (!IsDropDownOpen)
                {
                    SetCurrentValue(IsDropDownOpenProperty, true);
                }
                e.Handled = true;
                return;
            }

            if (IsDropDownOpen)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
                e.Handled = true;
            }
            else
            {
                PseudoClasses.Set(pcPressed, true);
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (!e.Handled && e.Source is Visual source)
            {
                if (_popup?.IsInsidePopup(source) == true)
                {
                    if (UpdateSelectionFromEventSource(e.Source))
                    {
                        _popup?.Close();
                        e.Handled = true;
                    }
                }
                else if (PseudoClasses.Contains(pcPressed))
                {
                    SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
                    e.Handled = true;
                }
            }

            PseudoClasses.Set(pcPressed, false);
            base.OnPointerReleased(e);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            if (_popup != null)
            {
                _popup.Opened -= PopupOpened;
                _popup.Closed -= PopupClosed;
            }

            if (_editableTextBox != null)
            {
                _editableTextBox.TextChanged -= OnTextBoxTextChanged;
                _editableTextBox.GotFocus -= OnTextBoxGotFocus;
                _editableTextBox.LostFocus -= OnTextBoxLostFocus;
            }

            _popup = e.NameScope.Get<Popup>("PART_Popup");
            _editableTextBox = e.NameScope.Get<TextBox>("PART_EditableTextBox");

            if (_popup != null)
            {
                _popup.Opened += PopupOpened;
                _popup.Closed += PopupClosed;
            }

            if (_editableTextBox != null)
            {
                _editableTextBox.TextChanged += OnTextBoxTextChanged;
                _editableTextBox.GotFocus += OnTextBoxGotFocus;
                _editableTextBox.LostFocus += OnTextBoxLostFocus;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == SelectedItemProperty)
            {
                UpdateTextFromSelectedItem();
                TryFocusSelectedItem();
            }
            else if (change.Property == IsDropDownOpenProperty)
            {
                PseudoClasses.Set(pcDropdownOpen, change.GetNewValue<bool>());
            }
            else if (change.Property == FilterTextProperty)
            {
                FilterItems();
            }
            else if (change.Property == ItemsSourceProperty)
            {
                AllItems = change.GetNewValue<IEnumerable>();
            }

            base.OnPropertyChanged(change);
        }

        private void OnTextBoxTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;

            var newText = _editableTextBox?.Text ?? string.Empty;
            SetCurrentValue(TextProperty, newText);
            SetCurrentValue(FilterTextProperty, newText);

            if (!IsDropDownOpen && !string.IsNullOrEmpty(newText))
            {
                SetCurrentValue(IsDropDownOpenProperty, true);
            }
        }

        private void OnTextBoxGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (!string.IsNullOrEmpty(FilterText))
            {
                SetCurrentValue(IsDropDownOpenProperty, true);
            }
        }

        private void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            // Update text to match selected item when losing focus
            if (!IsDropDownOpen)
            {
                UpdateTextFromSelectedItem();
            }
        }

        private void FilterItems()
        {
            if (AllItems == null) return;

            var filterText = FilterText;
            if (string.IsNullOrEmpty(filterText))
            {
                ItemsSource = AllItems;
                return;
            }

            var filterFunction = FilterFunction ?? DefaultFilterFunction;
            var filteredItems = AllItems.Cast<object>().Where(item => filterFunction(item, filterText)).ToList();
            
            ItemsSource = filteredItems;
        }

        private static bool DefaultFilterFunction(object? item, string? filterText)
        {
            if (item == null || string.IsNullOrEmpty(filterText))
                return true;

            return item.ToString()?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private void UpdateTextFromSelectedItem()
        {
            _isUpdatingText = true;
            try
            {
                var text = SelectedItem?.ToString() ?? string.Empty;
                SetCurrentValue(TextProperty, text);
                SetCurrentValue(FilterTextProperty, text);
                
                if (_editableTextBox != null)
                {
                    _editableTextBox.Text = text;
                }
            }
            finally
            {
                _isUpdatingText = false;
            }
        }

        private void PopupClosed(object? sender, EventArgs e)
        {
            _subscriptionsOnOpen.Clear();
            DropDownClosed?.Invoke(this, EventArgs.Empty);
        }

        private void PopupOpened(object? sender, EventArgs e)
        {
            TryFocusSelectedItem();

            _subscriptionsOnOpen.Clear();

            this.GetObservable(IsVisibleProperty).Subscribe(IsVisibleChanged).Equals(_subscriptionsOnOpen);

            foreach (var parent in this.GetVisualAncestors().OfType<Control>())
            {
                parent.GetObservable(IsVisibleProperty).Subscribe(IsVisibleChanged).Equals(_subscriptionsOnOpen);
            }

            DropDownOpened?.Invoke(this, EventArgs.Empty);
        }

        private void IsVisibleChanged(bool isVisible)
        {
            if (!isVisible && IsDropDownOpen)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }
        }

        private void TryFocusSelectedItem()
        {
            var selectedIndex = SelectedIndex;
            if (IsDropDownOpen && selectedIndex != -1)
            {
                var container = ContainerFromIndex(selectedIndex);

                if (container == null && SelectedIndex != -1)
                {
                    ScrollIntoView(Selection.SelectedIndex);
                    container = ContainerFromIndex(selectedIndex);
                }

                if (container != null && CanFocus(container))
                {
                    container.Focus();
                }
            }
        }

        private bool CanFocus(Control control) => control.Focusable && control.IsEffectivelyEnabled && control.IsVisible;

        private void SelectFocusedItem()
        {
            foreach (var dropdownItem in GetRealizedContainers())
            {
                if (dropdownItem.IsFocused)
                {
                    SelectedIndex = IndexFromContainer(dropdownItem);
                    break;
                }
            }
        }

        private void MoveFocus(int direction)
        {
            var containers = GetRealizedContainers().ToList();
            if (containers.Count == 0) return;

            var focusedIndex = -1;
            for (int i = 0; i < containers.Count; i++)
            {
                if (containers[i].IsFocused)
                {
                    focusedIndex = i;
                    break;
                }
            }

            var newIndex = focusedIndex + direction;
            if (newIndex < 0) newIndex = containers.Count - 1;
            if (newIndex >= containers.Count) newIndex = 0;

            if (newIndex >= 0 && newIndex < containers.Count)
            {
                containers[newIndex].Focus();
            }
        }

        internal void ItemFocused(SearchableComboBoxItem dropDownItem)
        {
            if (IsDropDownOpen && dropDownItem.IsFocused && dropDownItem.IsArrangeValid)
            {
                dropDownItem.BringIntoView();
            }
        }

        /// <summary>
        /// Clears the selection and text
        /// </summary>
        public void Clear()
        {
            SelectedItem = null;
            SelectedIndex = -1;
            SetCurrentValue(TextProperty, string.Empty);
            SetCurrentValue(FilterTextProperty, string.Empty);
        }
    }
}