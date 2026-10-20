function markLoaded(image) {
    image.closest(".artwork-frame").querySelector(".artwork-placeholder").hidden = true;
}

document.addEventListener("load", event => {
    if (event.target instanceof HTMLImageElement && event.target.classList.contains("external-artwork")) {
        markLoaded(event.target);
    }
}, true);

document.addEventListener("error", event => {
    if (event.target instanceof HTMLImageElement && event.target.classList.contains("external-artwork")) {
        event.target.hidden = true;
        event.target.closest(".artwork-frame").querySelector(".artwork-placeholder").hidden = false;
    }
}, true);

function checkArtwork() {
    document.querySelectorAll(".external-artwork").forEach(image => {
        if (image.complete) {
            if (image.naturalWidth > 0) {
                markLoaded(image);
            } else {
                image.hidden = true;
            }
        }
    });
}

checkArtwork();
Blazor.addEventListener("enhancedload", checkArtwork);
