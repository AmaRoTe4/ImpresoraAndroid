using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace PrintAgentAndroid.Ui;

public sealed class TestPanelBuilder
{
    public View Root { get; }

    public Action? OnQuickTextTest;
    public Action? OnQuickZplTest;
    public Action<string, bool>? OnSendCustom;

    private readonly Context _context;
    private readonly EditText _customInput;
    private readonly Button _sendButton;
    private readonly Button _typeTextBtn;
    private readonly Button _typeZplBtn;
    private readonly LinearLayout _lastSentCard;
    private readonly TextView _lastSentText;
    private bool _isZpl;

    public TestPanelBuilder(Context context)
    {
        _context = context;
        var scroll = new ScrollView(context);
        var column = new LinearLayout(context) { Orientation = Orientation.Vertical };
        column.SetPadding(0, 0, 0, Theme.DpToPxInt(context, 24));

        var quickLabel = new TextView(context) { Text = "Prueba rápida", TextSize = 12 };
        quickLabel.SetTextColor(new Color(Theme.MutedForeground));
        column.AddView(quickLabel);

        var grid = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        var gridParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Theme.DpToPxInt(context, 8)
        };
        var txtCard = BuildQuickCard("TXT", "Texto plano", "Imprime una línea de texto");
        var zplCard = BuildQuickCard("ZPL", "ZPL", "Etiqueta de prueba ZPL");
        txtCard.Click += (_, _) => OnQuickTextTest?.Invoke();
        zplCard.Click += (_, _) => OnQuickZplTest?.Invoke();
        var cardLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
        grid.AddView(txtCard, new LinearLayout.LayoutParams(cardLp) { MarginEnd = Theme.DpToPxInt(context, 6) });
        grid.AddView(zplCard, new LinearLayout.LayoutParams(cardLp) { MarginStart = Theme.DpToPxInt(context, 6) });
        column.AddView(grid, gridParams);

        var customLabel = new TextView(context) { Text = "Personalizado", TextSize = 12 };
        customLabel.SetTextColor(new Color(Theme.MutedForeground));
        customLabel.SetPadding(0, Theme.DpToPxInt(context, 20), 0, Theme.DpToPxInt(context, 8));
        column.AddView(customLabel);

        var customCard = new LinearLayout(context) { Orientation = Orientation.Vertical };
        var cardPad = Theme.DpToPxInt(context, 16);
        customCard.SetPadding(cardPad, cardPad, cardPad, cardPad);
        customCard.Background = ViewFactory.RoundedBackground(Theme.Muted, Theme.DpToPx(context, 16));

        var typeRow = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        _typeTextBtn = BuildTypeToggle("Texto plano");
        _typeZplBtn = BuildTypeToggle("ZPL");
        _typeTextBtn.Click += (_, _) => SetType(false);
        _typeZplBtn.Click += (_, _) => SetType(true);
        var typeLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
        typeRow.AddView(_typeTextBtn, new LinearLayout.LayoutParams(typeLp) { MarginEnd = Theme.DpToPxInt(context, 6) });
        typeRow.AddView(_typeZplBtn, new LinearLayout.LayoutParams(typeLp) { MarginStart = Theme.DpToPxInt(context, 6) });
        customCard.AddView(typeRow);

        _customInput = new EditText(context)
        {
            Hint = "Escribí el texto a imprimir...",
            InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine
        };
        _customInput.SetLines(4);
        _customInput.Gravity = GravityFlags.Top;
        _customInput.SetTextColor(new Color(Theme.Foreground));
        _customInput.SetHintTextColor(new Color(Theme.MutedForeground));
        _customInput.Background = ViewFactory.RoundedBackground(Theme.Background, Theme.DpToPx(context, 12));
        _customInput.SetPadding(cardPad, cardPad, cardPad, cardPad);
        _customInput.TextChanged += (_, _) => UpdateSendButtonState();
        var inputParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Theme.DpToPxInt(context, 12)
        };
        customCard.AddView(_customInput, inputParams);

        _sendButton = new Button(context) { Text = "Enviar prueba interna" };
        _sendButton.SetAllCaps(false);
        _sendButton.Click += (_, _) =>
        {
            var text = _customInput.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            OnSendCustom?.Invoke(text, _isZpl);
        };
        var sendParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Theme.DpToPxInt(context, 12)
        };
        customCard.AddView(_sendButton, sendParams);

        column.AddView(customCard, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Theme.DpToPxInt(context, 4)
        });

        _lastSentCard = new LinearLayout(context) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        _lastSentCard.SetPadding(cardPad, cardPad, cardPad, cardPad);
        _lastSentCard.Background = ViewFactory.RoundedBackground(Theme.AccentPillBg, Theme.DpToPx(context, 16));
        var lastSentLabel = new TextView(context) { Text = "Último enviado", TextSize = 12 };
        lastSentLabel.SetTextColor(new Color(Theme.Accent));
        _lastSentText = new TextView(context) { TextSize = 12 };
        _lastSentText.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        _lastSentText.SetTextColor(new Color(Theme.MutedForeground));
        _lastSentCard.AddView(lastSentLabel);
        _lastSentCard.AddView(_lastSentText);
        column.AddView(_lastSentCard, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Theme.DpToPxInt(context, 16)
        });

        SetType(false);
        UpdateSendButtonState();

        scroll.AddView(column);
        Root = scroll;
    }

    public void ShowLastSent(string content, bool isZpl)
    {
        _lastSentCard.Visibility = ViewStates.Visible;
        _lastSentText.Text = $"[{(isZpl ? "ZPL" : "TEXTO")}] {content}";
    }

    public void ClearCustomInput() => _customInput.Text = string.Empty;

    private void SetType(bool isZpl)
    {
        _isZpl = isZpl;
        _typeTextBtn.Background = ViewFactory.RoundedBackground(!isZpl ? Theme.AccentPillBg : Theme.Muted, Theme.DpToPx(_context, 10));
        _typeZplBtn.Background = ViewFactory.RoundedBackground(isZpl ? Theme.AccentPillBg : Theme.Muted, Theme.DpToPx(_context, 10));
        _typeTextBtn.SetTextColor(new Color(!isZpl ? Theme.Accent : Theme.MutedForeground));
        _typeZplBtn.SetTextColor(new Color(isZpl ? Theme.Accent : Theme.MutedForeground));
        _customInput.Hint = isZpl ? "^XA^FO50,50^ADN,36,20^FD...^FS^XZ" : "Escribí el texto a imprimir...";
    }

    private void UpdateSendButtonState()
    {
        var hasText = !string.IsNullOrWhiteSpace(_customInput.Text);
        _sendButton.Enabled = hasText;
        _sendButton.Background = ViewFactory.RoundedBackground(hasText ? Theme.Accent : Theme.Muted, Theme.DpToPx(_context, 12));
        _sendButton.SetTextColor(new Color(hasText ? Theme.AccentForeground : Theme.MutedForeground));
    }

    private Button BuildTypeToggle(string label)
    {
        var btn = new Button(_context) { Text = label, TextSize = 12 };
        btn.SetAllCaps(false);
        var pad = Theme.DpToPxInt(_context, 8);
        btn.SetPadding(pad, pad, pad, pad);
        return btn;
    }

    private LinearLayout BuildQuickCard(string badge, string title, string desc)
    {
        var card = new LinearLayout(_context) { Orientation = Orientation.Vertical, Clickable = true, Focusable = true };
        card.SetGravity(GravityFlags.CenterHorizontal);
        var pad = Theme.DpToPxInt(_context, 16);
        card.SetPadding(pad, pad, pad, pad);
        card.Background = ViewFactory.RoundedBackground(Theme.Muted, Theme.DpToPx(_context, 16));

        var badgeCircle = new TextView(_context) { Text = badge, TextSize = 11, Gravity = GravityFlags.Center };
        badgeCircle.SetTypeface(badgeCircle.Typeface, TypefaceStyle.Bold);
        badgeCircle.SetTextColor(new Color(Theme.Accent));
        badgeCircle.Background = ViewFactory.CirclePillBackground(Theme.AccentPillBg);
        var badgeParams = new LinearLayout.LayoutParams(Theme.DpToPxInt(_context, 40), Theme.DpToPxInt(_context, 40));
        card.AddView(badgeCircle, badgeParams);

        var titleTv = new TextView(_context) { Text = title, TextSize = 13 };
        titleTv.SetTypeface(titleTv.Typeface, TypefaceStyle.Bold);
        titleTv.SetTextColor(new Color(Theme.Foreground));
        titleTv.SetPadding(0, Theme.DpToPxInt(_context, 8), 0, 0);
        card.AddView(titleTv);

        var descTv = new TextView(_context) { Text = desc, TextSize = 11, Gravity = GravityFlags.Center };
        descTv.SetTextColor(new Color(Theme.MutedForeground));
        descTv.SetPadding(0, Theme.DpToPxInt(_context, 2), 0, 0);
        card.AddView(descTv);

        return card;
    }
}
