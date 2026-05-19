# Map Asset Keys

`DistrictMapView` can resolve map `assetKey` values to optional PNG files in this folder.

Resolution order:

- `business.shop` -> `res://assets/map/business/shop.png`
- `business.shop` -> `res://assets/map/business.shop.png`

If no PNG exists, the map keeps using fallback primitives.
