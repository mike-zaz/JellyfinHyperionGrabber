# Changelog

## [0.9.0](https://github.com/mike-zaz/JellyfinHyperionGrabber/compare/v0.8.0...v0.9.0) (2026-11-07)


### Features

* find Hyperion.ng and HyperHDR servers on the network ([#73](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/73)) ([1303930](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/13039302aed3df94bb6b7b5271f8353ea5785d01))
* follow playback speeds from 0.25x to 2x ([#67](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/67)) ([a2a5868](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/a2a586875bd2ad9da356ce7b9455374f1ec449d4))
* light timing offset per device, with a test video to calibrate it ([#65](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/65)) ([843c3c1](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/843c3c11d1622cb3fe22195acf9d00b9ccc3f767))
* live preview of the picture sent to Hyperion on the plugin page ([#69](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/69)) ([d53a446](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/d53a44695acd6108c5e1b0ee01239ea6ae540708))
* skip libraries, tagged videos, and trailers and extras ([#71](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/71)) ([0bc02fc](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/0bc02fcc8059ceea1a3ff0a67aa864230c85ed7f))


### Bug Fixes

* keep HDR colors by tone mapping HDR10 and HLG with zscale ([#52](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/52)) ([8db9915](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/8db99157cf4afdd38743cfc4dd21a6f8ab27cf3d))
* point links and downloads to the renamed GitHub account ([#60](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/60)) ([c6bd5fa](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/c6bd5fa661f15977448773472168f61fc114d521))


### Documentation

* Bump mkdocs-material from 9.7.7 to 9.8.0 in /docs ([#58](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/58)) ([d854f35](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/d854f356b1c1e4fccd0c341b8e1b6923d2400118))
* lead with Ambilight, compare setups and refresh the landing page ([#61](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/61)) ([e559ff0](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/e559ff03f2304d153938c5424d2ae8862c947ca8))
* show how to add HyperHDR next to Jellyfin, ask for the status panel in bug reports ([#63](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/63)) ([3e0d4b7](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/3e0d4b7534cb944a430261baf2c10256abb5c245))


### Dependencies

* Bump the testing group with 1 update ([#59](https://github.com/mike-zaz/JellyfinHyperionGrabber/issues/59)) ([300aa08](https://github.com/mike-zaz/JellyfinHyperionGrabber/commit/300aa0861c71fbbb7745fa90e734cafab148046f))

## [0.8.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.7.1...v0.8.0) (2026-10-20)


### Features

* one Hyperion server per TV (several targets) ([a7c70c4](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/a7c70c42c06b5838b2ca0cffd6f2ac4fe2d2c1dc))
* send anamorphic video in its display shape; black bars via Hyperion's detector ([a7c70c4](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/a7c70c42c06b5838b2ca0cffd6f2ac4fe2d2c1dc))
* tone map HDR10/HLG to SDR and fix Dolby Vision colors ([a7c70c4](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/a7c70c42c06b5838b2ca0cffd6f2ac4fe2d2c1dc))

## [0.7.1](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.7.0...v0.7.1) (2026-10-19)


### Bug Fixes

* re-register when Hyperion.ng asks for it ([#40](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/40)) ([03ddc45](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/03ddc45b7a8a78e27419f7b68faed3573745dd1c))


### Dependencies

* Bump the testing group with 5 updates ([#38](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/38)) ([1437ec6](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/1437ec6fe9d346809ddbabad82ea55f3028c654b))

## [0.7.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.6.0...v0.7.0) (2026-10-11)


### Features

* read the playback position from Kodi over JSON-RPC (precise timing) ([#33](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/33)) ([ed7c059](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/ed7c059e0c91190533c5c10490d2bce0263792a2))

## [0.6.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.5.0...v0.6.0) (2026-10-11)


### Features

* status panel on the configuration page ([#31](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/31)) ([d260360](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/d2603604b5204c497e6ab95076c0fa5f5a528551))

## [0.5.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.4.0...v0.5.0) (2026-10-09)


### Features

* reconnect to Hyperion with backoff ([#28](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/28)) ([ab53823](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/ab53823ffdd117464b69bf5d06ba194fd3e6b848))

## [0.4.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.3.0...v0.4.0) (2026-10-07)


### Features

* track the playback position as a range so rounded and rare reports stop shifting the lights ([#26](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/26)) ([c61e2c2](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/c61e2c2c501c0d7685fb40b96bf0e38324eba3a4))

## [0.3.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.2.0...v0.3.0) (2026-10-05)


### Features

* light timing offset and release the lights after a long pause ([#23](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/23)) ([3f41757](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/3f417571a9c6603ceae1d8b9a0dd6f7cf60f7ac0))

## [0.2.0](https://github.com/25LioN52/JellyfinHyperionGrabber/compare/v0.1.0...v0.2.0) (2026-10-05)


### Features

* ffmpeg frame source with hardware acceleration ([#15](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/15)) ([81550af](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/81550afe2f13a3effa955f5a646eecf58ff6d341))
* playback monitor with device and user filter ([#14](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/14)) ([11a3a26](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/11a3a2679395fa2482a787bfbc625bb4960ef1db))
* stream frames to Hyperion during playback ([#20](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/20)) ([2d369c5](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/2d369c5063be48d2a27d9b23d7a2babdca207298))


### Bug Fixes

* use an existing Jellyfin 12 image tag in the dev test bed ([#17](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/17)) ([75c0a80](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/75c0a802f7970a581e1c3c0df11b80a2f8d8c644))

## 0.1.0 (2026-10-04)


### Features

* foundation for the Hyperion Grabber plugin (M0) ([49f0aa5](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/49f0aa5d74e45092ec9d45c3af000bac6358e473))


### Bug Fixes

* close the Hyperion connection gracefully so the final Clear is not lost ([ebcaf3c](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/ebcaf3ce3265c8a9d612f47bf083c8c1d86edc15))


### Documentation

* Bump mkdocs-material from 9.7.6 to 9.7.7 in /docs ([#2](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/2)) ([b043505](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/b043505c2e121b781e080ae01b427f265b3c37b6))
* Update mkdocs requirement from &lt;2,&gt;=1.6 to &gt;=1.6.1,&lt;2 in /docs ([#1](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/1)) ([7db352f](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/7db352f01e9ec2d369c712c7ac43a6fe3a014dd6))


### Dependencies

* Bump NSubstitute from 5.3.0 to 6.2.0 ([#4](https://github.com/25LioN52/JellyfinHyperionGrabber/issues/4)) ([2e3c0d1](https://github.com/25LioN52/JellyfinHyperionGrabber/commit/2e3c0d1688e2a49352d61e40ea035edd331aff63))

## Changelog

All notable changes to this project are documented here. Entries are generated by
[release-please](https://github.com/googleapis/release-please) from [Conventional Commits](https://www.conventionalcommits.org/);
do not edit released sections by hand. Versions follow [Semantic Versioning](https://semver.org/); the fourth
component of the plugin version identifies the Jellyfin line (`.0` = 10.11, `.1` = 12).
