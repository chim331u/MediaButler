#!/bin/bash
RES_DIR="/Users/luca/GitHub/mediabutler/MediaButler/src/android/app/src/main/res"
SRC_IMG="/Users/luca/GitHub/mediabutler/MediaButler/docs/Butler with Play Button Tray.png"

echo "Rimuovo i vecchi file xml dell'icona adattiva..."
rm -f "$RES_DIR/mipmap-anydpi-v26/ic_launcher.xml"
rm -f "$RES_DIR/mipmap-anydpi-v26/ic_launcher_round.xml"
rm -f "$RES_DIR/drawable/ic_launcher_background.xml"
rm -f "$RES_DIR/drawable/ic_launcher_foreground.xml"

echo "Generazione icone PNG per le varie densità..."

# MDPI (48x48)
echo "Generazione MDPI (48x48)..."
rm -f "$RES_DIR/mipmap-mdpi/ic_launcher.webp" "$RES_DIR/mipmap-mdpi/ic_launcher_round.webp"
sips -z 48 48 "$SRC_IMG" --out "$RES_DIR/mipmap-mdpi/ic_launcher.png"
cp "$RES_DIR/mipmap-mdpi/ic_launcher.png" "$RES_DIR/mipmap-mdpi/ic_launcher_round.png"

# HDPI (72x72)
echo "Generazione HDPI (72x72)..."
rm -f "$RES_DIR/mipmap-hdpi/ic_launcher.webp" "$RES_DIR/mipmap-hdpi/ic_launcher_round.webp"
sips -z 72 72 "$SRC_IMG" --out "$RES_DIR/mipmap-hdpi/ic_launcher.png"
cp "$RES_DIR/mipmap-hdpi/ic_launcher.png" "$RES_DIR/mipmap-hdpi/ic_launcher_round.png"

# XHDPI (96x96)
echo "Generazione XHDPI (96x96)..."
rm -f "$RES_DIR/mipmap-xhdpi/ic_launcher.webp" "$RES_DIR/mipmap-xhdpi/ic_launcher_round.webp"
sips -z 96 96 "$SRC_IMG" --out "$RES_DIR/mipmap-xhdpi/ic_launcher.png"
cp "$RES_DIR/mipmap-xhdpi/ic_launcher.png" "$RES_DIR/mipmap-xhdpi/ic_launcher_round.png"

# XXHDPI (144x144)
echo "Generazione XXHDPI (144x144)..."
rm -f "$RES_DIR/mipmap-xxhdpi/ic_launcher.webp" "$RES_DIR/mipmap-xxhdpi/ic_launcher_round.webp"
sips -z 144 144 "$SRC_IMG" --out "$RES_DIR/mipmap-xxhdpi/ic_launcher.png"
cp "$RES_DIR/mipmap-xxhdpi/ic_launcher.png" "$RES_DIR/mipmap-xxhdpi/ic_launcher_round.png"

# XXXHDPI (192x192)
echo "Generazione XXXHDPI (192x192)..."
rm -f "$RES_DIR/mipmap-xxxhdpi/ic_launcher.webp" "$RES_DIR/mipmap-xxxhdpi/ic_launcher_round.webp"
sips -z 192 192 "$SRC_IMG" --out "$RES_DIR/mipmap-xxxhdpi/ic_launcher.png"
cp "$RES_DIR/mipmap-xxxhdpi/ic_launcher.png" "$RES_DIR/mipmap-xxxhdpi/ic_launcher_round.png"

echo "Icone Android generate con successo!"
