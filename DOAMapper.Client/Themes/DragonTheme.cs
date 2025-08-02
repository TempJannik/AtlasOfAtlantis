using MudBlazor;

namespace DOAMapper.Client.Themes;

public static class DragonTheme
{
    public static MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight()
        {
            // Primary colors - Dragon brown theme
            Primary = "#8b4513",           // --dragon-primary
            PrimaryContrastText = "#ffffff",
            PrimaryLighten = "#a0522d",    // --dragon-primary-light
            PrimaryDarken = "#654321",     // --dragon-primary-dark
            
            // Secondary colors - Complementary to dragon theme
            Secondary = "#a0522d",         // --dragon-primary-light
            SecondaryContrastText = "#ffffff",
            SecondaryLighten = "#cd853f",
            SecondaryDarken = "#8b4513",
            
            // Tertiary colors
            Tertiary = "#654321",          // --dragon-primary-dark
            TertiaryContrastText = "#ffffff",
            
            // Background colors
            Background = "#1a1a1a",        // --dragon-bg-primary
            Surface = "rgba(0, 0, 0, 0.2)", // --dragon-bg-surface
            
            // Text colors
            TextPrimary = "#e0e0e0",       // --dragon-text-primary
            TextSecondary = "#a0a0a0",     // --dragon-text-secondary
            TextDisabled = "#666666",      // --dragon-text-muted
            
            // Action colors
            ActionDefault = "#a0a0a0",     // --dragon-text-secondary
            ActionDisabled = "#666666",    // --dragon-text-muted
            ActionDisabledBackground = "rgba(0, 0, 0, 0.1)",
            
            // Divider colors
            Divider = "rgba(139, 69, 19, 0.3)", // --dragon-border-primary
            DividerLight = "rgba(139, 69, 19, 0.1)",
            
            // Table colors
            TableLines = "rgba(139, 69, 19, 0.3)",
            TableStriped = "rgba(139, 69, 19, 0.05)",
            TableHover = "rgba(139, 69, 19, 0.1)",
            
            // Drawer colors
            DrawerBackground = "#2d2d2d",  // --dragon-bg-secondary
            DrawerText = "#e0e0e0",       // --dragon-text-primary
            DrawerIcon = "#a0a0a0",       // --dragon-text-secondary
            
            // AppBar colors
            AppbarBackground = "#2d2d2d",  // --dragon-bg-secondary
            AppbarText = "#e0e0e0",       // --dragon-text-primary
            
            // Success, Info, Warning, Error colors (keeping standard for accessibility)
            Success = "#22c55e",
            SuccessContrastText = "#ffffff",
            SuccessLighten = "#86efac",
            SuccessDarken = "#16a34a",
            
            Info = "#3b82f6",
            InfoContrastText = "#ffffff",
            InfoLighten = "#93c5fd",
            InfoDarken = "#2563eb",
            
            Warning = "#f59e0b",
            WarningContrastText = "#ffffff",
            WarningLighten = "#fbbf24",
            WarningDarken = "#d97706",
            
            Error = "#dc2626",
            ErrorContrastText = "#ffffff",
            ErrorLighten = "#fca5a5",
            ErrorDarken = "#b91c1c",
            
            // Dark theme indicator
            Dark = "#1a1a1a"
        },
        
        PaletteDark = new PaletteDark()
        {
            // Use same colors for dark theme since dragon theme is already dark
            Primary = "#8b4513",
            PrimaryContrastText = "#ffffff",
            PrimaryLighten = "#a0522d",
            PrimaryDarken = "#654321",
            
            Secondary = "#a0522d",
            SecondaryContrastText = "#ffffff",
            SecondaryLighten = "#cd853f",
            SecondaryDarken = "#8b4513",
            
            Tertiary = "#654321",
            TertiaryContrastText = "#ffffff",
            
            Background = "#1a1a1a",
            Surface = "rgba(0, 0, 0, 0.2)",
            
            TextPrimary = "#e0e0e0",
            TextSecondary = "#a0a0a0",
            TextDisabled = "#666666",
            
            ActionDefault = "#a0a0a0",
            ActionDisabled = "#666666",
            ActionDisabledBackground = "rgba(0, 0, 0, 0.1)",
            
            Divider = "rgba(139, 69, 19, 0.3)",
            DividerLight = "rgba(139, 69, 19, 0.1)",
            
            TableLines = "rgba(139, 69, 19, 0.3)",
            TableStriped = "rgba(139, 69, 19, 0.05)",
            TableHover = "rgba(139, 69, 19, 0.1)",
            
            DrawerBackground = "#2d2d2d",
            DrawerText = "#e0e0e0",
            DrawerIcon = "#a0a0a0",
            
            AppbarBackground = "#2d2d2d",
            AppbarText = "#e0e0e0",
            
            Success = "#22c55e",
            SuccessContrastText = "#ffffff",
            SuccessLighten = "#86efac",
            SuccessDarken = "#16a34a",
            
            Info = "#3b82f6",
            InfoContrastText = "#ffffff",
            InfoLighten = "#93c5fd",
            InfoDarken = "#2563eb",
            
            Warning = "#f59e0b",
            WarningContrastText = "#ffffff",
            WarningLighten = "#fbbf24",
            WarningDarken = "#d97706",
            
            Error = "#dc2626",
            ErrorContrastText = "#ffffff",
            ErrorLighten = "#fca5a5",
            ErrorDarken = "#b91c1c",
            
            Dark = "#1a1a1a"
        },
        
        LayoutProperties = new LayoutProperties()
        {
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "300px",
            AppbarHeight = "64px",
            DefaultBorderRadius = "12px"  // --dragon-radius-md
        },
        

        
        Shadows = new Shadow()
        {
            Elevation = new string[]
            {
                "none",
                "0 2px 8px rgba(139, 69, 19, 0.3)",      // --dragon-shadow-sm
                "0 4px 16px rgba(139, 69, 19, 0.4)",     // --dragon-shadow-md
                "0 8px 32px rgba(139, 69, 19, 0.3)",     // --dragon-shadow-lg
                "0 12px 40px rgba(0, 0, 0, 0.3)",        // --dragon-shadow-xl
                "0 0 0 3px rgba(139, 69, 19, 0.2), 0 8px 32px rgba(139, 69, 19, 0.15)", // --dragon-shadow-glow
                "0 16px 48px rgba(0, 0, 0, 0.4)",
                "0 20px 56px rgba(0, 0, 0, 0.5)",
                "0 24px 64px rgba(0, 0, 0, 0.6)",
                "0 28px 72px rgba(0, 0, 0, 0.7)",
                "0 32px 80px rgba(0, 0, 0, 0.8)",
                "0 36px 88px rgba(0, 0, 0, 0.9)",
                "0 40px 96px rgba(0, 0, 0, 1.0)",
                "0 44px 104px rgba(0, 0, 0, 1.0)",
                "0 48px 112px rgba(0, 0, 0, 1.0)",
                "0 52px 120px rgba(0, 0, 0, 1.0)",
                "0 56px 128px rgba(0, 0, 0, 1.0)",
                "0 60px 136px rgba(0, 0, 0, 1.0)",
                "0 64px 144px rgba(0, 0, 0, 1.0)",
                "0 68px 152px rgba(0, 0, 0, 1.0)",
                "0 72px 160px rgba(0, 0, 0, 1.0)",
                "0 76px 168px rgba(0, 0, 0, 1.0)",
                "0 80px 176px rgba(0, 0, 0, 1.0)",
                "0 84px 184px rgba(0, 0, 0, 1.0)"
            }
        },
        
        ZIndex = new ZIndex()
        {
            Drawer = 1200,
            AppBar = 1100,
            Snackbar = 1400,
            Tooltip = 1500
        }
    };
}
