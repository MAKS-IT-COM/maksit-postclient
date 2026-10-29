# Microsoft Store listings

The app UI is English, Italian, French, German, or Spanish. Partner Center needs a Store listing for each of those languages. Paste only the fenced text. The MSIX package language stays `en-us`: the app chooses the UI language at runtime, so the package does not ship satellite resources.

Partner Center Store ID is `9N7F29XPK5W4` (see [CONTRIBUTING.md](../../CONTRIBUTING.md)). The Store deep link and Web Store URL appear after the product is live.

| Partner Center language | Code | Listing |
|-------------------------|------|---------|
| English (United States) | `en-us` | [description](en-us/description.md) |
| Italian (Italy) | `it-it` | [description](it-it/description.md) |
| French (France) | `fr-fr` | [description](fr-fr/description.md) |
| German (Germany) | `de-de` | [description](de-de/description.md) |
| Spanish (Spain) | `es-es` | [description](es-es/description.md) |

Each folder also has `product-features.md`, `keywords.md`, and `additional-info.md`. Copyright, the license URL, and Developed by are the same in every language.

Shared across languages:

| Field | Document |
|-------|----------|
| Store logos | [logos.md](logos.md) |
| Restricted capability `runFullTrust` | [restricted-capabilities.md](restricted-capabilities.md) |

Category is set once, under Properties: Productivity. Hardware checkboxes stay in [CONTRIBUTING.md](../../CONTRIBUTING.md) under **System requirements (Properties)**.

Leave **What's new in this version** empty on the first submission for every language.
