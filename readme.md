## UABEANext

UABEA with dock support. When this repo becomes stable and reaches feature parity, the code will be merged into the original UABEA repo.

Nightly:

- https://nightly.link/nesrak1/UABEANext/workflows/build-windows/master/uabea-windows.zip
- https://nightly.link/nesrak1/UABEANext/workflows/build-ubuntu/master/uabea-ubuntu.zip

Report any issues on the original repo: https://github.com/nesrak1/UABEA

---

## 📥 Download My Enhanced Build

This fork provides a pre-compiled version with all the optimizations and Spanish localization included:

- **[Download UABEANext4 Enhanced (Windows x64)](https://github.com/AtaraxyInDev/UABEANext/releases/download/v1.0.0/UABEANext4-Enhanced-Win64.zip)**

---

## 🚀 Enhanced Features in this Fork

This fork introduces significant improvements to **Localization**, **Performance**, and **Processing Engines**.

### 🌍 Full Localization Support (EN/ES)
- **New I18n Framework**: Dynamic language switching without application restart.
- **English & Spanish**: 100% UI localization coverage for both languages.
- **Docking Localization**: Full support for tool windows (Float, Dock, Auto Hide, Close).
- **Localized Plugins**: Texture and TextAsset plugins are fully integrated into the translation system.

### ⚡ Engine Optimizations
- **Parallel Processing**: Multi-threaded Batch Import/Export using `Parallel.ForEach`.
- **Advanced Texture Caching**: LRU caching system in `TextureLoader` to reduce CPU/RAM overhead when processing large atlases.
- **High-Performance Rendering**: Integrated **SkiaSharp** for ultra-fast sprite cropping and complex mesh rendering.
- **Silent UI Updates**: Eliminated lag during mass asset processing by optimizing UI thread interactions.

### 🛠️ Technical Improvements
- **Stability**: Resolved several critical crashes and resource dictionary duplicates.
- **Clean Git Workflow**: Updated `.gitignore` to exclude build artifacts and executables.

---

## 💡 Motivation & Acknowledgments

This project is a personal endeavor driven by the need for a more efficient workflow in game localization.

### My Story
As a professional **game translator**, I often faced significant bottlenecks when working with Unity games. Handling massive amounts of assets (averaging **20,000+ items** per project) caused the original tools to freeze or take an excessive amount of time to import/export. I wanted to solve this problem to improve my productivity and, at the same time, bring the tool to my native language (Spanish) to help the community.

### 🤖 AI-Powered Development (Vibecoding)
I am a **"Vibecoder"**. I have realized these architectural and performance improvements by collaborating entirely with advanced AI models:
- **Claude Opus 4.6**
- **Gemini 3.1 Pro (High)**


*Note: I understand that this method of development may not be standard for all maintainers. If you decide not to merge this contribution due to its AI-generated nature, I completely respect that. This fork will remain available for anyone who needs these specific optimizations and localization.*


