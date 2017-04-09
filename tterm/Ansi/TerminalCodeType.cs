namespace tterm.Ansi
{
    internal enum TerminalCodeType
    {
        Text,
        Bell,
        LineFeed,
        CarriageReturn,
        Backspace,
        Tab,
        ShiftOut,
        ShiftIn,
        SaveCursor,
        Reset,
        SetTitle,
        RestoreCursor,
        TabSet,

        SetCursorPosition,
        CursorCharAbsolute,
        CursorUp,
        CursorDown,
        CursorForward,
        CursorBackward,
        RestoreCursorPosition,
        EraseDisplay,
        EraseInLine,
        SetGraphicsMode,
        SetMode,
        CharAttributes,
        ResetMode,
    }
}
