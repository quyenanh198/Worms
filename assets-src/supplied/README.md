# User-supplied waiting screen art

`coastal/` is extracted from `segments (1).zip`; `neon/` is extracted from `segments.zip`, both supplied by the project owner. The original PNG bytes and names are preserved here. No executable or document instructions from the archives are used.

`client/Assets/Resources/Waiting/` contains selected scene layers used by the randomized opening card. Seven separate accessory PNGs are copied to `client/Assets/Resources/Cosmetics/` as purchasable hat-slot items (IDs 18–24). The remaining source cutouts stay here for later compositions. The existing menu and store run after the opening card is dismissed.

The two full background images in `assets-src/generated/runtime/waiting/` were generated from these source cutouts to fill transparent gaps in the segmented scenery. Their runtime copies sit in `Resources/Waiting/Coastal/` and `Resources/Waiting/Neon/`; the supplied logos, worms, accessories and buttons remain separate foreground sprites.

Previewing the composition exposed a rectangular sky patch behind the coastal title, duplicate neon title fragments, and transparent holes in the wooden platform. Isolated title cutouts and a repaired platform were generated from the supplied pieces and are stored beside the backgrounds in `assets-src/generated/runtime/waiting/`. The original pieces remain untouched here.

The opening screen now shows a four-worm red, blue, yellow and green squad in both themes. The original coastal red/green and neon green cutouts remain unchanged; five additional transparent color variants are in `assets-src/generated/runtime/waiting/` and their playable copies in `client/Assets/Resources/Waiting/`.
