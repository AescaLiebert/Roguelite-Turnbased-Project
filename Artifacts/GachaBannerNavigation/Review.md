# Gacha banner navigation

Fixed the null reference caused by deleted UXML labels. GachaBannerView.Refresh now treats rates, guarantee text/fill, pull captions and notice as optional. Modal close/title/binding also tolerate removed elements.

The left column is a vertical ScrollView of banner thumbnail cards. Selected cards have a gold border and SELECTED text. Selection updates promotional artwork, title, pool icons, rates/details, selector candidates and the configuration passed to paid pulls. Invalid cards are disabled with a reason tooltip. Pull prices, rates and account-wide selector progress retain their existing rules.

The scene currently has one authored KOF configuration. Add new SummonBannerConfigurationSO assets to Scene-Gacha > GachaBannerUI > FrontEndController > Available Banners. The existing Banner Configuration is included once. Optional poster, thumbnail and description are authored on each configuration. Cards use poster art or a pool icon if no thumbnail is assigned; promotions use a pool fighter on the shared arena if no poster is assigned.

The reference informed the thumbnail rail, dominant illustration and bottom action placement. Rate summaries remain in Details. Large decorative/status blocks and fixed 667-pixel body sizing were removed. Optional empty descriptions/notices are hidden.

Unity 6000.3.4f1 compiled the current runtime/editor sources. Actual UI Toolkit captures at 1920×1080, 1280×720 and 768×1024 were visually inspected for clipping, text legibility, artwork, left navigation and pull controls. The long-list screenshots contain unsaved layout samples solely to inspect overflow; they were not added as game banners. No summons, account saves or scene saves were performed. Automated tests were not run.

See Capture.txt for viewport geometry and UnityCapture.log for import/compilation output. Inspect actual multi-banner selection, purchase/results, Details/Escape and selector claim in Play Mode when more banner assets are authored.
