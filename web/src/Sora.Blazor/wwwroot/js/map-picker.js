let leafletPromise;
const maps = new WeakMap();

function loadLeaflet() {
    if (window.L) return Promise.resolve(window.L);
    if (leafletPromise) return leafletPromise;

    leafletPromise = new Promise((resolve, reject) => {
        const cssUrl = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css";
        if (!document.querySelector(`link[href="${cssUrl}"]`)) {
            const link = document.createElement("link");
            link.rel = "stylesheet";
            link.href = cssUrl;
            document.head.appendChild(link);
        }

        const script = document.createElement("script");
        script.src = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
        script.onload = () => resolve(window.L);
        script.onerror = () => reject(new Error("Leaflet could not be loaded"));
        document.head.appendChild(script);
    }).catch(error => { leafletPromise = null; throw error; });
    return leafletPromise;
}

export async function initialize(element, latitude, longitude, hasSelection, dotNet) {
    const L = await loadLeaflet();
    const map = L.map(element).setView([latitude, longitude], hasSelection ? 16 : 12);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap</a> contributors'
    }).addTo(map);

    let marker = null;
    const placeMarker = (latlng) => {
        if (marker) marker.setLatLng(latlng);
        else marker = L.marker(latlng, { draggable: true }).addTo(map);
        marker.off("dragend").on("dragend", () => {
            const point = marker.getLatLng();
            dotNet.invokeMethodAsync("SelectCoordinates", point.lat, point.lng);
        });
        dotNet.invokeMethodAsync("SelectCoordinates", latlng.lat, latlng.lng);
    };

    if (hasSelection) {
        marker = L.marker([latitude, longitude], { draggable: true }).addTo(map);
        marker.on("dragend", () => {
            const point = marker.getLatLng();
            dotNet.invokeMethodAsync("SelectCoordinates", point.lat, point.lng);
        });
    }
    map.on("click", event => placeMarker(event.latlng));
    maps.set(element, { map, markerRef: () => marker, placeMarker });
    window.setTimeout(() => map.invalidateSize(), 120);
}

export function destroy(element) {
    const state = maps.get(element);
    if (state) {
        state.map.remove();
        maps.delete(element);
    }
}
