using System.Windows;
using System.Windows.Controls;
using bams.desktop.Utils;

namespace bams.desktop.Behaviors;

/// <summary>
/// Adds live thousands grouping to a money <see cref="TextBox"/> without changing its bound numeric value.
/// </summary>
public static class MoneyInput
{
    private static readonly string[] MoneyPropertyMarkers =
        ["Amount", "Balance", "Cash", "Fee", "Limit"];

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(MoneyInput));

    /// <summary>Enables live comma grouping and focus-loss normalization for a money text box.</summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(MoneyInput),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// Marks a text box as monetary regardless of its binding path name (e.g. <c>VaultActual</c>).
    /// Set by the <c>Input.Money</c> style so explicitly money-styled fields are always formatted.
    /// </summary>
    public static readonly DependencyProperty IsMoneyFieldProperty =
        DependencyProperty.RegisterAttached("IsMoneyField", typeof(bool), typeof(MoneyInput), new PropertyMetadata(false));

    public static bool GetIsMoneyField(DependencyObject element) => (bool)element.GetValue(IsMoneyFieldProperty);

    public static void SetIsMoneyField(DependencyObject element, bool value) => element.SetValue(IsMoneyFieldProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if ((bool)eventArgs.OldValue)
        {
            textBox.TextChanged -= OnTextChanged;
            textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
        }

        if ((bool)eventArgs.NewValue)
        {
            textBox.TextChanged += OnTextChanged;
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
            if (IsMoneyTextBox(textBox))
            {
                FormatTextWhilePreservingCaret(textBox);
            }
        }
    }

    // Formats after all typing and paste paths, then maps the caret through inserted or removed commas.
    private static void OnTextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (sender is TextBox textBox &&
            !(bool)textBox.GetValue(IsUpdatingProperty) &&
            IsMoneyTextBox(textBox))
        {
            FormatTextWhilePreservingCaret(textBox);
        }
    }

    // A completed valid value is shown consistently with all read-only money displays.
    private static void OnLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs eventArgs)
    {
        if (sender is TextBox textBox &&
            IsMoneyTextBox(textBox) &&
            MoneyText.TryParse(textBox.Text, out var amount))
        {
            UpdateText(textBox, MoneyText.Format(amount), textBox.Text.Length);
        }
    }

    private static void FormatTextWhilePreservingCaret(TextBox textBox)
    {
        var originalText = textBox.Text;
        var formattedText = MoneyText.FormatWhileEditing(originalText);
        if (formattedText == originalText)
        {
            return;
        }

        var caret = GetFormattedCaretPosition(originalText, formattedText, textBox.SelectionStart);
        UpdateText(textBox, formattedText, caret);
    }

    private static void UpdateText(TextBox textBox, string text, int caret)
    {
        textBox.SetValue(IsUpdatingProperty, true);
        try
        {
            textBox.Text = text;
            textBox.SelectionStart = Math.Clamp(caret, 0, text.Length);
            textBox.SelectionLength = 0;
        }
        finally
        {
            textBox.SetValue(IsUpdatingProperty, false);
        }
    }

    private static int GetFormattedCaretPosition(string originalText, string formattedText, int originalCaret)
    {
        var meaningfulCharacterCount = originalText[..Math.Min(originalCaret, originalText.Length)]
            .Count(character => char.IsDigit(character) || character is '.' or '-');

        // A caret before every meaningful character stays at the start instead of jumping to the end.
        if (meaningfulCharacterCount == 0)
        {
            return 0;
        }

        var seen = 0;

        for (var index = 0; index < formattedText.Length; index++)
        {
            if (char.IsDigit(formattedText[index]) || formattedText[index] is '.' or '-')
            {
                seen++;
            }

            if (seen == meaningfulCharacterCount)
            {
                return index + 1;
            }
        }

        return formattedText.Length;
    }

    // Keeps the shared input style safe for non-financial fields such as account numbers and phone numbers.
    // Explicit money fields always qualify; the shared Input.Text style falls back to binding-path markers.
    private static bool IsMoneyTextBox(TextBox textBox)
    {
        if (GetIsMoneyField(textBox))
        {
            return true;
        }

        var path = textBox.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path;
        return path is not null && MoneyPropertyMarkers.Any(marker =>
            path.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
