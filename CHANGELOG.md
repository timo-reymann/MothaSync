# 1.0.0 (2026-10-17)


### Bug Fixes

* **client:** handle failed sync runs and retries ([ea0291c](https://github.com/timo-reymann/MothaSync/commit/ea0291c88988e27994f0179b6e5ce47e128199ab))
* **client:** show the main window before the tray opens the license dialog ([e982487](https://github.com/timo-reymann/MothaSync/commit/e982487286391f1efd2f327c3bdefc86c5e301a2))


### Features

* add initial SyncMotha solution with Avalonia client UI mock ([8a2ca12](https://github.com/timo-reymann/MothaSync/commit/8a2ca125891e55ddaa04a380285a60185309c4ec))
* **client:** add cross-platform system tray icon ([144baae](https://github.com/timo-reymann/MothaSync/commit/144baae9168ff743dfb4a774f4ede5e59d92d9cc))
* **client:** add current application artwork ([50daa46](https://github.com/timo-reymann/MothaSync/commit/50daa468332bef037bf1962f262b4659bd187e16))
* **client:** add license information dialog ([62b682c](https://github.com/timo-reymann/MothaSync/commit/62b682c4275abd791f2d2f3560f2c0f3913dcf01))
* **client:** add new logo, drive artwork and status tray icons ([8c61db2](https://github.com/timo-reymann/MothaSync/commit/8c61db24e145efb8c8a6d4716f80f13a5effb330))
* **client:** add server URL field to settings ([acf837f](https://github.com/timo-reymann/MothaSync/commit/acf837ff780465c70d981ed2df13e3bf52d27fa7))
* **client:** add sync path comparison helper ([9051140](https://github.com/timo-reymann/MothaSync/commit/90511403ae4e24785552706c77686ac122d7df71))
* **client:** allow text selection and copy in the license dialog ([4a3b7ef](https://github.com/timo-reymann/MothaSync/commit/4a3b7ef67460d62b1ef96c26758458dd3cb336aa))
* **client:** bundle a NOTICE file for the license dialog ([4049fff](https://github.com/timo-reymann/MothaSync/commit/4049fffc84d737c14c59527e33e2fb599d6eeb53))
* **client:** bundle the generated NOTICE in the license dialog ([fe3c890](https://github.com/timo-reymann/MothaSync/commit/fe3c8901b1931ac69637351b9963774a66006411))
* **client:** improve transfer log details ([4efe571](https://github.com/timo-reymann/MothaSync/commit/4efe571f6f73a8ea42de2247982db59147bab748))
* **client:** persist and render real sync history logs ([78274a3](https://github.com/timo-reymann/MothaSync/commit/78274a3d3254f22ec5596862b6b544d9347879c8))
* **client:** register release builds for autostart ([0027188](https://github.com/timo-reymann/MothaSync/commit/0027188de80bcc9119a1e6247e36d442c8b63e55))
* **client:** replace template with sync app ([5e50e11](https://github.com/timo-reymann/MothaSync/commit/5e50e11087260156a8fdde192368ec6823819f9e))
* **client:** report sync outcomes and notify users ([df8e0d6](https://github.com/timo-reymann/MothaSync/commit/df8e0d690c64d13cf5865167dcf63ca07fa5b582))
* **client:** run syncs through the rclone REST API ([82e676d](https://github.com/timo-reymann/MothaSync/commit/82e676d222a930b411dea5408f701fb946c9ebbb))
* **client:** scroll the license text horizontally ([f8036a0](https://github.com/timo-reymann/MothaSync/commit/f8036a0bbecd02b2646967b639def9a83100a033))
* **client:** show application version in UI ([ef47789](https://github.com/timo-reymann/MothaSync/commit/ef47789c52a3630de3f47b461f5d44718816a21f))
* **client:** swap in new artwork, add error states and same-folder check ([310e7e6](https://github.com/timo-reymann/MothaSync/commit/310e7e6f59414593212a0626bbbb1160f8f2cb2e))
* **client:** validate duplicate sync folders in the UI ([0181d0b](https://github.com/timo-reymann/MothaSync/commit/0181d0be47d3d7b585bc63b5f24c6fdb3736814b))
* distribute releases via 0install feed and Windows bootstrapper ([7a7e648](https://github.com/timo-reymann/MothaSync/commit/7a7e6484c01262d75c6b3d24a3d51db020184385))
* **server:** add a compose sample with Prometheus ([bc1c48e](https://github.com/timo-reymann/MothaSync/commit/bc1c48ee85ee73a8e0df38811133896129af0ad9))
* **server:** build a chiseled container image ([ece7ab7](https://github.com/timo-reymann/MothaSync/commit/ece7ab77938b7e168e3135b93f69e0f1d32f1015))
* **server:** create the last-run gauges at device registration ([175c402](https://github.com/timo-reymann/MothaSync/commit/175c402dfc410090a01249d71c7a4cc869a5358d))
* **server:** expose Prometheus metrics on dedicated port ([929fef6](https://github.com/timo-reymann/MothaSync/commit/929fef6d6801f72d0c468ebb914813d3cdbea75c))
* **server:** replace template with device enrollment and run reporting API ([687d51b](https://github.com/timo-reymann/MothaSync/commit/687d51b14a0f2fe00dac94fac2c2aa9869b2a28f))
* **server:** restore per-device last-run gauges on startup ([7b13415](https://github.com/timo-reymann/MothaSync/commit/7b1341585f1194c53e0b5d55b034a93aa625e31a))
* **shared:** add protobuf contracts and run log models ([9436a30](https://github.com/timo-reymann/MothaSync/commit/9436a3029d4995f6959655732f7908f986cdf676))
