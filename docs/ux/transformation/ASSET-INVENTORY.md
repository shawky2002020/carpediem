# Carpediem asset inventory

## Sources and coverage

The signed-in Google Drive connector listed the supplied Mango Tree folder and every nested folder on 25 November 2026. The source folder contains the campsite presentation, 43 campsite stills and 79 activity files. The connector folder listing supplied the full inventory where image search was partial. [Mango Tree source folder](https://drive.google.com/drive/folders/1d0vlOPgu-K_EirBpIOnIPkaqYXhKWtbx)

| Source | Count | Review |
| --- | ---: | --- |
| The Mango Tree Campsite.pptx | 1 | Downloaded the 396,669,831-byte PowerPoint through the signed-in Drive browser after the connector and Google Slides hit their size limits. Inspected the 12 slide descriptions, the cover preview, all 20 embedded stills and graphics, and a representative frame from each of the two videos. |
| Camspite | 43 | All file records listed. There are 37 JPEGs, 3 HEIF images and 3 PNGs. Six existing project crops were inspected visually. |
| Competitive Games | 4 | One still sampled, people in an orchard game |
| Construction | 2 | One still sampled, people assembling materials outdoors |
| Outdoor Ethics | 4 | One still sampled, people preparing supplies outdoors |
| Arts & Crafts | 2 | One still sampled, people at a group craft table |
| Water Games | 3 | One still sampled, people on the activity grounds |
| Color Fight | 3 | One still sampled, people on the orchard path |
| Night Activities | 41 | 36 stills and 5 videos. One group still sampled. |
| Campfire | 1 | One still sampled, group around a campfire |
| Zipline | 5 | One still sampled, person on the zipline |
| Cooking | 6 | One still sampled, people preparing food |
| Planting | 3 | One still sampled, people preparing seedlings |
| Pool | 2 | One still sampled, people in the pool |
| Scouting | 3 | One still sampled, people at an outdoor activity |

The photos and deck show painted jade huts, deep green mango canopy, ochre soil, red and violet doors, timber bunks and the communal hall's colored textiles. The deck also shows the orchard entry, shaded picnic benches and the campsite at dusk. Each activity sample contains people and several show younger participants. The two inspected video frames also show people. Activity imagery informed the team-based tone and stays outside the app. [Campsite presentation](https://drive.google.com/file/d/1QXxidlVswM9FjbHbYoPqMg63UrP1bQEh/view) · [Prior visual and privacy audit](../ASSET_AUDIT.md)

## Runtime brand assets

| Source | File | Delivery |
| --- | --- | --- |
| Official campsite badge | `frontend/public/logo.svg` | Existing scalable Angular logo. Use fixed inline dimensions. An empty `alt` suits a decorative mark next to a visible Carpediem wordmark. |
| Official campsite badge | `frontend/public/logo-128.png` | Existing 128 × 128 PNG, 29,872 bytes. A fresh render of the root `logo.pdf` confirmed the same design, including the tent, trees, red landscape and forest badge. |
| Closing-slide campsite signature | `frontend/public/brand/camp-wordmark.webp` | Faithful 448 × 239 RGBA lossless WebP export of `ppt/media/image19.emf` from slide 12, 1,700 bytes. Welcome displays the cream signature on evergreen with `alt="Carpe Diem"`. Desktop sign-in and registration reuse it decoratively beside the existing Carpediem masthead. The image carries intrinsic dimensions and stays out of the compact mobile account form. |
| Interface and display fonts | `frontend/public/fonts/` | Existing self-hosted Hanken Grotesk and Bricolage Grotesque files ship with their respective OFL licenses. |

## Optimized campsite candidates

These six source photos were visually inspected through the existing optimized crops. The selected crops show places and furnishings without recognizable people. All 27 AVIF, WebP and JPEG variants are already available for local review in `design-assets/review/`. The manifest records source file IDs, exact dimensions, bytes and hashes. [Existing asset registry](../ASSET_REGISTRY.md) · [Derivative manifest](../../../design-assets/review/manifest.json)

| Candidate | Crop and AVIF size | Potential placement | Alt text when informative |
| --- | --- | --- | --- |
| Painted hut with pink door (`Huts.png`) | 480 × 600 at 48,954 bytes, 768 × 960 at 105,011 bytes | Welcome on a tall phone viewport | Mango-painted hut with a pink door |
| Same hut under the canopy (`Hut 1.jpg`) | 1200 × 675 at 149,564 bytes, 1600 × 900 at 237,070 bytes | Desktop welcome or confirmed campsite location | Painted hut beneath the mango trees |
| Flower hut (`Hut 2.jpg`) | 1200 × 675 at 133,831 bytes | Event detail where the location is verified | Flower-painted campsite hut with a violet door |
| Orchard (`IMG_4016.png`) | 480 × 600 at 54,418 bytes, 768 × 960 at 133,966 bytes | Entry or onboarding with a tall crop | Mango trees along a sandy path |
| Wooden bunks (`Hut Inside 1.jpg`) | 1200 × 675 at 57,550 bytes | Verified campsite facilities | Wooden bunk beds inside a campsite hut |
| Community hall (`P1655568.jpg`) | 1200 × 675 at 69,987 bytes | Verified campsite facilities | Colorful floor seating in the campsite hall |

The project registry records photo usage rights as **unknown** and holds the derivatives outside runtime. The project still requires owner confirmation of app reuse for the specific photos. Activity media with identifiable subjects also requires applicable consent and context review. The exported closing-slide brand signature is the sole supplied Drive asset in Angular's public directory. [Existing asset registry](../ASSET_REGISTRY.md)

On rights clearance, package only the chosen variants locally. Set intrinsic dimensions, use the portrait crop on narrow viewports, and position the hut doorway or grove opening as the focal point. Load a welcome image eagerly only if it sits above the fold. Lazy-load images farther down the page. Attach facility imagery only to verified locations so a campsite photo never implies that an unrelated event takes place there.
