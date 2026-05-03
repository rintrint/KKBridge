# KKBridge

A BepInEx plugin for Koikatsu that exports Timeline animations to VMD format, compatible with MikuMikuDance and Blender (via [MMD Tools](https://github.com/MMD-Blender/blender_mmd_tools)).

## Notes

* Use Koikatu instead of KoikatuSunshine for better stability. If you encounter issues with KoikatuSunshine, try Koikatu.
* KKBridge is designed based on [Tda式初音ミク・アペンドVer1.10](https://bowlroll.net/file/4576). To prevent animation glitches, please ensure your model's bone structure is compatible with the Tda standard.

## Requirements

* Visual Studio 2022

## How to Build

Build `src/KKBridge.sln` with Visual Studio.

## Dependencies

* BepInEx
* 0Harmony
* Timeline
* DLLs from Koikatsu
* SimpleJson

## Screenshots

### 1. Export button

![export_01_button](docs/images/export_01_button.png)

### 2. Export settings

![export_02_settings](docs/images/export_02_settings.png)

### 3. Result in MMD

![export_03_mmd](docs/images/export_03_mmd.png)

### 4. Result in Blender

![export_04_blender](docs/images/export_04_blender.png)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributors

KKBridge is developed by [@rintrint](https://github.com/rintrint). See the [list of contributors](https://github.com/rintrint/KKBridge/graphs/contributors).
