# BusLane GitHub Pages

This folder contains the GitHub Pages website for the BusLane project.

## 🚀 Quick Setup

### Option 1: Enable GitHub Pages (Recommended)

1. Go to your repository **Settings** → **Pages**
2. Under "Source", select **Deploy from a branch**
3. Select `main` branch and `/docs` folder
4. Click **Save**

Your site will be live at: `https://soliktomasz.github.io/BusLane/`

### Option 2: Manual Asset Copy

If images don't load, copy the icon manually:

```bash
cp ../Assets/icon.png assets/icon.png
```

## 📁 Structure

```
docs/
├── index.html      # Main landing page (single-file, self-contained)
├── assets/
│   └── icon.png    # App icon (optional - falls back to GitHub raw URL)
├── .nojekyll       # Prevents Jekyll processing
└── README.md       # This file
```

## ✨ Features

The landing page includes:

- **Hero section** with animated gradient backgrounds
- **Features grid** showcasing BusLane capabilities
- **Platform cards** for macOS, Windows, and Linux
- **Installation tabs** for download vs build from source
- **Security section** highlighting encryption features
- **Call-to-action** with download and sponsor links
- **Responsive design** for mobile and desktop
- **Dark theme** matching modern developer tools

## 🎨 Customization

The website is a single HTML file with embedded CSS and JavaScript. To customize:

### Colors
Edit the CSS variables in `:root`:
```css
:root {
    --primary: #0078D4;       /* Azure blue */
    --accent: #FF8C00;        /* Orange */
    --bg-dark: #0f1419;       /* Background */
    --text-primary: #ffffff;   /* Main text */
}
```

### Content
- Update feature descriptions in the `.feature-card` elements
- Modify platform support in the `.platform-card` elements
- Change installation commands in the code blocks
- Update links to releases and documentation

## 🔧 Technical Details

- **No build step required** - Pure HTML, CSS, and JavaScript
- **External dependencies**: 
  - Google Fonts (Inter)
  - Lucide Icons (via CDN)
- **Fallback images**: Uses GitHub raw URLs if local assets missing
- **Smooth animations**: CSS keyframes and transitions
- **Accessible**: Semantic HTML with ARIA labels

## 📱 Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile browsers (responsive design)
