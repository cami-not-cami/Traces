// Client-side static script for the Trip Dashboard (Itinerary Planner)
// Reads server-provided configuration from the global window.TripConfig object

let map;

// Read config properties
const googleApiKey = window.TripConfig.googleApiKey;
const lat = window.TripConfig.latitude;
const lng = window.TripConfig.longitude;
const hasCoords = window.TripConfig.hasCoords;
const places = window.TripConfig.places;

async function showPlaceDetailsPopup(placeId) {
    if (!placeId) {
        console.warn("No Google Place ID provided.");
        return;
    }

    const modal = document.getElementById("place-details-modal");
    const card = document.getElementById("place-details-card");
    const loading = document.getElementById("modal-loading");
    const content = document.getElementById("modal-content");

    // Show modal wrapper and animate card scale/opacity
    modal.classList.remove("hidden");
    setTimeout(() => {
        modal.classList.remove("opacity-0");
        modal.classList.add("opacity-100");
        card.classList.remove("scale-95", "opacity-0");
        card.classList.add("scale-100", "opacity-100");
    }, 10);

    loading.classList.remove("hidden");
    content.classList.add("hidden");

    try {
        const response = await fetch(`/Home/PlaceDetails?placeId=${encodeURIComponent(placeId)}`);
        if (!response.ok) throw new Error("Failed to fetch place details.");
        const data = await response.json();

        populatePlaceDetails(data);
    } catch (error) {
        console.error("Error loading place details:", error);
        document.getElementById("modal-title").textContent = "Error Loading Details";
        document.getElementById("modal-address").textContent = "Could not fetch details for this location.";
        loading.classList.add("hidden");
        content.classList.remove("hidden");
    }
}

function populatePlaceDetails(data) {
    const loading = document.getElementById("modal-loading");
    const content = document.getElementById("modal-content");

    // Title & Category
    const titleEl = document.getElementById("modal-title");
    const catEl = document.getElementById("modal-category");
    const name = data.displayName?.text || "Unnamed Place";
    titleEl.textContent = name;

    // Try to extract category
    let category = "Attraction";
    if (data.types) {
        if (data.types.includes('restaurant') || data.types.includes('food') || data.types.includes('cafe')) {
            category = 'Food & Drink';
        } else if (data.types.includes('lodging')) {
            category = 'Lodging';
        } else if (data.types.includes('transit_station') || data.types.includes('subway_station') || data.types.includes('train_station')) {
            category = 'Transit';
        } else if (data.types.includes('shopping_mall') || data.types.includes('store')) {
            category = 'Shopping';
        }
    }
    catEl.textContent = category;

    // Image
    const imgEl = document.getElementById("modal-cover-photo");
    if (data.photos && data.photos.length > 0) {
        const photoName = data.photos[0].name;
        imgEl.src = `https://places.googleapis.com/v1/${photoName}/media?key=${googleApiKey}&maxWidthPx=800`;
    } else {
        imgEl.src = `https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=800&q=80`;
    }

    // Rating & Stars
    const ratingNumEl = document.getElementById("modal-rating-num");
    const ratingCountEl = document.getElementById("modal-rating-count");
    const starsContainer = document.getElementById("modal-stars");
    starsContainer.innerHTML = "";

    const rating = data.rating || 0;
    ratingNumEl.textContent = rating.toFixed(1);
    ratingCountEl.textContent = `(${data.userRatingCount || 0} reviews)`;

    // Draw Stars
    const fullStars = Math.floor(rating);
    const hasHalf = rating % 1 >= 0.5;
    for (let i = 1; i <= 5; i++) {
        let starSvg = "";
        if (i <= fullStars) {
            starSvg = `<svg class="w-5 h-5 fill-current" viewBox="0 0 20 20"><path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/></svg>`;
        } else if (i === fullStars + 1 && hasHalf) {
            starSvg = `<svg class="w-5 h-5 fill-current viewBox="0 0 20 20"><defs><linearGradient id="halfStar"><stop offset="50%" stop-color="currentColor"/><stop offset="50%" stop-color="#cbd5e1"/></linearGradient></defs><path fill="url(#halfStar)" d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/></svg>`;
        } else {
            starSvg = `<svg class="w-5 h-5 fill-current text-slate-200" viewBox="0 0 20 20"><path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/></svg>`;
        }
        starsContainer.innerHTML += starSvg;
    }

    // Address
    document.getElementById("modal-address").textContent = data.formattedAddress || "No address provided.";

    // Phone
    const phoneContainer = document.getElementById("modal-phone-container");
    const phoneEl = document.getElementById("modal-phone");
    if (data.nationalPhoneNumber || data.internationalPhoneNumber) {
        phoneEl.textContent = data.nationalPhoneNumber || data.internationalPhoneNumber;
        phoneContainer.classList.remove("hidden");
    } else {
        phoneContainer.classList.add("hidden");
    }

    // Website
    const websiteContainer = document.getElementById("modal-website-container");
    const websiteEl = document.getElementById("modal-website");
    if (data.websiteUri) {
        websiteEl.href = data.websiteUri;
        websiteEl.textContent = data.websiteUri.replace(/^https?:\/\/(www\.)?/, '').split('/')[0];
        websiteContainer.classList.remove("hidden");
    } else {
        websiteContainer.classList.add("hidden");
    }

    // Editorial Summary
    const summaryContainer = document.getElementById("modal-summary-container");
    const summaryEl = document.getElementById("modal-summary");
    if (data.editorialSummary && data.editorialSummary.text) {
        summaryEl.textContent = data.editorialSummary.text;
        summaryContainer.classList.remove("hidden");
    } else {
        summaryContainer.classList.add("hidden");
    }

    // Opening Hours
    const hoursContainer = document.getElementById("modal-hours-container");
    const hoursEl = document.getElementById("modal-hours");
    hoursEl.innerHTML = "";
    if (data.regularOpeningHours && data.regularOpeningHours.weekdayDescriptions) {
        data.regularOpeningHours.weekdayDescriptions.forEach(desc => {
            const parts = desc.split(": ");
            const day = parts[0];
            const time = parts[1] || "";
            hoursEl.innerHTML += `<div class="flex justify-between border-b border-slate-50 pb-1"><span class="font-semibold text-slate-700">${day}</span><span class="text-slate-500">${time}</span></div>`;
        });
        hoursContainer.classList.remove("hidden");
    } else {
        hoursContainer.classList.add("hidden");
    }

    // Close opening hours list by default
    const hoursList = document.getElementById("modal-hours");
    const arrow = document.getElementById("hours-arrow");
    hoursList.classList.add("hidden");
    arrow.classList.remove("rotate-180");

    // Reviews
    const reviewsContainer = document.getElementById("modal-reviews-container");
    const reviewsEl = document.getElementById("modal-reviews");
    reviewsEl.innerHTML = "";
    if (data.reviews && data.reviews.length > 0) {
        data.reviews.forEach(review => {
            let reviewStars = "";
            const r = review.rating || 0;
            for (let i = 1; i <= 5; i++) {
                if (i <= r) {
                    reviewStars += `<svg class="w-3.5 h-3.5 fill-current text-amber-400" viewBox="0 0 20 20"><path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/></svg>`;
                } else {
                    reviewStars += `<svg class="w-3.5 h-3.5 fill-current text-slate-200" viewBox="0 0 20 20"><path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z"/></svg>`;
                }
            }

            const authorPhoto = review.authorAttribution?.photoUri || "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=32&h=32";
            const authorName = review.authorAttribution?.displayName || "Anonymous";
            const timeDesc = review.relativePublishTimeDescription || "";
            const text = review.text?.text || "";

            reviewsEl.innerHTML += `
                <div class="pt-4 first:pt-0 space-y-2">
                    <div class="flex items-center space-x-2.5">
                        <img class="w-8 h-8 rounded-full border border-slate-100 object-cover" src="${authorPhoto}" alt="${authorName}" onerror="this.src='https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=32&h=32'" />
                        <div>
                            <div class="text-xs font-bold text-slate-800">${authorName}</div>
                            <div class="flex items-center space-x-1.5 mt-0.5">
                                <div class="flex">${reviewStars}</div>
                                <span class="text-[10px] text-slate-400 font-semibold">${timeDesc}</span>
                            </div>
                        </div>
                    </div>
                    <p class="text-xs text-slate-600 leading-relaxed pl-10">${text}</p>
                </div>
            `;
        });
        reviewsContainer.classList.remove("hidden");
    } else {
        reviewsContainer.classList.add("hidden");
    }

    loading.classList.add("hidden");
    content.classList.remove("hidden");
}

function closePlaceDetailsModal() {
    const modal = document.getElementById("place-details-modal");
    const card = document.getElementById("place-details-card");

    card.classList.remove("scale-100", "opacity-100");
    card.classList.add("scale-95", "opacity-0");
    modal.classList.remove("opacity-100");
    modal.classList.add("opacity-0");

    setTimeout(() => {
        modal.classList.add("hidden");
    }, 300);
}

function toggleModalHours() {
    const hours = document.getElementById("modal-hours");
    const arrow = document.getElementById("hours-arrow");
    if (hours.classList.contains("hidden")) {
        hours.classList.remove("hidden");
        arrow.classList.add("rotate-180");
    } else {
        hours.classList.add("hidden");
        arrow.classList.remove("rotate-180");
    }
}

document.addEventListener("DOMContentLoaded", () => {
    document.addEventListener("click", (e) => {
        const modal = document.getElementById("place-details-modal");
        const card = document.getElementById("place-details-card");
        if (modal && !modal.classList.contains("hidden")) {
            const clickedInsideCard = card.contains(e.target);
            const clickedTrigger = e.target.closest('[onclick^="showPlaceDetailsPopup"]');
            if (!clickedInsideCard && !clickedTrigger) {
                closePlaceDetailsModal();
            }
        }
    });

    document.addEventListener("keydown", (e) => {
        const modal = document.getElementById("place-details-modal");
        if (e.key === "Escape" && modal && !modal.classList.contains("hidden")) {
            closePlaceDetailsModal();
        }
    });
});

function enterBadgeEditMode() {
    document.getElementById("trip-badge-view").classList.add("hidden");
    document.getElementById("trip-badge-edit").classList.remove("hidden");
    document.getElementById("editTripLocationInput").focus();
}

function exitBadgeEditMode() {
    document.getElementById("trip-badge-view").classList.remove("hidden");
    document.getElementById("trip-badge-edit").classList.add("hidden");
}

function openInviteModal() {
    const modal = document.getElementById("invite-member-modal");
    if (modal) {
        modal.classList.remove("hidden");
    }
}

function closeInviteModal() {
    const modal = document.getElementById("invite-member-modal");
    if (modal) {
        modal.classList.add("hidden");
        const emailInput = document.getElementById("email");
        if (emailInput) emailInput.value = "";
    }
}

// Initialize badge autocomplete
document.addEventListener("DOMContentLoaded", () => {
    const input = document.getElementById("editTripLocationInput");
    const dropdown = document.getElementById("edit-trip-suggestions");
    const form = document.getElementById("trip-badge-edit-form");
    let debounceTimer;

    if (input && dropdown) {
        input.addEventListener("input", function () {
            clearTimeout(debounceTimer);
            const query = this.value.trim();

            // Reset hidden fields when typing new location
            document.getElementById("editTripPlaceId").value = '';
            document.getElementById("editTripLat").value = '';
            document.getElementById("editTripLng").value = '';
            document.getElementById("editTripAddress").value = '';
            document.getElementById("editTripPlaceName").value = '';

            if (query.length < 3) {
                dropdown.innerHTML = '';
                dropdown.classList.add('hidden');
                return;
            }

            debounceTimer = setTimeout(() => {
                fetchBadgeSuggestions(query, dropdown, input);
            }, 300);
        });

        document.addEventListener('click', function (e) {
            if (e.target !== input && !dropdown.contains(e.target)) {
                dropdown.innerHTML = '';
                dropdown.classList.add('hidden');
            }
        });
    }

    async function fetchBadgeSuggestions(query, dropdown, input) {
        try {
            let url = `/Home/Autocomplete?textInput=${encodeURIComponent(query)}`;
            const response = await fetch(url);
            if (!response.ok) throw new Error("Request failed");
            const data = await response.json();
            renderBadgeDropdown(data.suggestions || [], dropdown, input);
        } catch (error) {
            console.error("Error fetching suggestions", error);
        }
    }

    function renderBadgeDropdown(suggestions, dropdown, input) {
        dropdown.innerHTML = '';
        if (suggestions.length === 0) {
            dropdown.classList.add('hidden');
            return;
        }

        suggestions.forEach(item => {
            const textValue = item.placePrediction.text.text;
            const placeId = item.placePrediction.placeId;

            const row = document.createElement('div');
            row.className = 'px-4 py-2.5 text-sm text-slate-700 hover:bg-indigo-55 cursor-pointer transition-colors';
            row.textContent = textValue;

            row.addEventListener('click', async () => {
                input.value = textValue;
                dropdown.innerHTML = '';
                dropdown.classList.add('hidden');

                document.getElementById("editTripPlaceId").value = placeId;

                // Fetch details from backend
                try {
                    const detailsResponse = await fetch(`/Home/PlaceDetails?placeId=${placeId}`);
                    if (detailsResponse.ok) {
                        const details = await detailsResponse.json();
                        document.getElementById("editTripLat").value = details.location?.latitude || '';
                        document.getElementById("editTripLng").value = details.location?.longitude || '';
                        document.getElementById("editTripAddress").value = details.formattedAddress || '';
                        document.getElementById("editTripPlaceName").value = details.displayName?.text || textValue;
                    }
                } catch (error) {
                    console.error("Error fetching place details", error);
                }
            });

            dropdown.appendChild(row);
        });

        dropdown.classList.remove('hidden');
    }

    if (form) {
        form.addEventListener("submit", function (e) {
            e.preventDefault();

            const placeId = document.getElementById("editTripPlaceId").value || window.TripConfig.firstPlaceId;
            const startDate = document.getElementById("editTripStartDate").value;
            const endDate = document.getElementById("editTripEndDate").value;
            const description = document.getElementById("editTripDescription").value;
            const locationText = document.getElementById("editTripLocationInput").value;

            if (!locationText.trim()) {
                showAlertModal("Validation", "Please select a location.");
                return;
            }

            const tripId = window.TripConfig.tripId;

            if (tripId === 0) {
                // Unsaved trip: redirect to re-render client-side GET view with new parameters
                window.location.href = `/Trip?placeId=${encodeURIComponent(placeId)}&startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`;
            } else {
                // Saved trip
                $.ajax({
                    url: window.TripConfig.updateDetailsUrl,
                    type: "PATCH",
                    data: {
                        tripId: tripId,
                        title: locationText,
                        description: description,
                        startDate: startDate,
                        endDate: endDate,
                        googlePlaceId: placeId,
                        placeName: document.getElementById("editTripPlaceName").value,
                        latitude: document.getElementById("editTripLat").value,
                        longitude: document.getElementById("editTripLng").value,
                        address: document.getElementById("editTripAddress").value
                    },
                    success: function (data, textStatus, xhr) {
                        location.reload();
                    },
                    error: function (xhr, textStatus, errorThrown) {
                        showAlertModal('Error', 'Error updating trip: ' + xhr.status);
                    }
                });
                exitBadgeEditMode();
            }
        });
    }

    const inviteForm = document.getElementById("invite-member-form");
    if (inviteForm) {
        inviteForm.addEventListener("submit", function (e) {
            e.preventDefault();
            const email = document.getElementById("email").value;
            const tripId = window.TripConfig.tripId;
            const inviteUrl = window.TripConfig.inviteMemberUrl;

            if (!email) {
                showAlertModal("Validation", "Please enter a valid email address.");
                return;
            }

            $.ajax({
                url: inviteUrl,
                type: "POST",
                data: { tripId: tripId, email: email },
                success: function (res) {
                    if (res.success) {
                        showAlertModal("Success", "Member invited successfully!");
                        closeInviteModal();
                        location.reload();
                    } else {
                        showAlertModal("Error", res.message || "Failed to invite member.");
                    }
                },
                error: function (xhr) {
                    showAlertModal("Error", "Error: " + (xhr.responseText || "Could not complete the request."));
                }
            });
        });
    }
});

function saveBudget(tripId) {
    const budget = document.getElementById("budget").value;
    $.ajax({
        url: window.TripConfig.setBudgetUrl,
        type: "POST",
        data: { tripId: tripId, budget: budget },
        success: function (data, textStatus, xhr) {
            console.log('Budget updated successfully:', xhr.status);
        },
        error: function (xhr, textStatus, errorThrown) {
            showAlertModal('Error', 'Error saving budget: ' + xhr.status);
        }
    });
}

async function initMap() {
    const { Map } = await google.maps.importLibrary("maps");
    map = new Map(document.getElementById("map"), {
        center: { lat: lat, lng: lng },
        zoom: hasCoords ? 13 : 8,
    });

    // Add markers for all places
    const bounds = new google.maps.LatLngBounds();
    let hasMarkers = false;

    const infowindow = new google.maps.InfoWindow();

    places.forEach(p => {
        const marker = new google.maps.Marker({
            position: { lat: p.lat, lng: p.lng },
            map: map,
            title: p.name
        });

        marker.addListener("click", () => {
            infowindow.setContent(`<div class="p-2 font-sans font-semibold text-slate-800 text-xs">${p.name}</div>`);
            map.setZoom(20);
            map.setCenter(marker.position);
            infowindow.open(map, marker);
        });

        bounds.extend(marker.getPosition());
        hasMarkers = true;
    });

    if (hasMarkers && places.length > 1) {
        map.fitBounds(bounds);
    }

    // Draw route polylines on the map if present
    if (window.TripConfig.routes && window.TripConfig.routes.length > 0) {
        window.TripConfig.routes.forEach(route => {
            if (route.polyline) {
                const path = google.maps.geometry.encoding.decodePath(route.polyline);

                let strokeColor = "#0b03a6"; //blue for DRIVE
                let icons = null;

                if (route.travelMode === "WALK") {
                    strokeColor = "#f707d3"; // pink for WALK
                    // Render walking routes as dashed lines
                    icons = [{
                        icon: {
                            path: 'M 0,-1 0,1',
                            strokeOpacity: 1,
                            scale: 2
                        },
                        offset: '0',
                        repeat: '10px'
                    }];
                }

                const polylineOptions = {
                    path: path,
                    geodesic: true,
                    strokeColor: strokeColor,
                    strokeOpacity: route.travelMode === "WALK" ? 0 : 0.8,
                    strokeWeight: 4,
                    map: map
                };

                if (icons) {
                    polylineOptions.icons = icons;
                }

                new google.maps.Polyline(polylineOptions);
            }
        });
    }

    // Initialize custom autocomplete for all inline day forms using the backend Places API (New)
    document.querySelectorAll('.day-autocomplete').forEach(input => {
        const form = input.closest('form');
        const dropdown = form.querySelector('.suggestions-dropdown');
        let debounceTimer;

        input.addEventListener('input', function () {
            clearTimeout(debounceTimer);
            const query = this.value.trim();

            // Reset hidden fields
            form.querySelector('input[name="GooglePlaceId"]').value = '';
            form.querySelector('input[name="Latitude"]').value = '';
            form.querySelector('input[name="Longitude"]').value = '';
            form.querySelector('input[name="FormattedAddress"]').value = '';

            if (query.length < 3) {
                dropdown.innerHTML = '';
                dropdown.classList.add('hidden');
                return;
            }

            debounceTimer = setTimeout(() => {
                fetchSuggestions(query, dropdown, input);
            }, 300);
        });

        // Hide dropdown when clicking outside
        document.addEventListener('click', function (e) {
            if (e.target !== input && !dropdown.contains(e.target)) {
                dropdown.innerHTML = '';
                dropdown.classList.add('hidden');
            }
        });
    });

    async function fetchSuggestions(query, dropdown, input) {
        try {
            let url = `/Home/Autocomplete?textInput=${encodeURIComponent(query)}`;
            if (hasCoords) {
                url += `&latitude=${lat}&longitude=${lng}`;
            }
            const response = await fetch(url);
            if (!response.ok) throw new Error("Request failed");
            const data = await response.json();

            renderDropdown(data.suggestions || [], dropdown, input);
        } catch (error) {
            console.error("Error fetching suggestions", error);
        }
    }

    function renderDropdown(suggestions, dropdown, input) {
        dropdown.innerHTML = '';

        if (suggestions.length === 0) {
            dropdown.classList.add('hidden');
            return;
        }

        suggestions.forEach(item => {
            const textValue = item.placePrediction.text.text;
            const placeId = item.placePrediction.placeId;

            const row = document.createElement('div');
            row.className = 'px-4 py-2.5 text-sm text-slate-700 hover:bg-indigo-50 cursor-pointer transition-colors';
            row.textContent = textValue;

            row.addEventListener('click', async () => {
                input.value = textValue;
                dropdown.innerHTML = '';
                dropdown.classList.add('hidden');

                const form = input.closest('form');
                form.querySelector('input[name="GooglePlaceId"]').value = placeId;

                // Fetch details from backend
                try {
                    const detailsResponse = await fetch(`/Home/PlaceDetails?placeId=${placeId}`);
                    if (detailsResponse.ok) {
                        const details = await detailsResponse.json();

                        // Set coordinates and other hidden fields
                        const latitudeVal = details.location?.latitude || '';
                        const longitudeVal = details.location?.longitude || '';
                        const addressVal = details.formattedAddress || '';
                        const displayNameVal = details.displayName?.text || textValue;

                        form.querySelector('input[name="Latitude"]').value = latitudeVal;
                        form.querySelector('input[name="Longitude"]').value = longitudeVal;
                        form.querySelector('input[name="FormattedAddress"]').value = addressVal;
                        form.querySelector('input[name="PlaceName"]').value = displayNameVal;

                        // Autofill category if possible
                        const select = form.querySelector('select[name="Category"]');
                        if (details.types && (details.types.includes('restaurant') || details.types.includes('food') || details.types.includes('cafe'))) {
                            select.value = 'Food';
                        } else if (details.types && details.types.includes('lodging')) {
                            select.value = 'Lodging';
                        } else if (details.types && details.types.includes('transit_station')) {
                            select.value = 'Transit';
                        } else if (details.types && (details.types.includes('shopping_mall') || details.types.includes('store'))) {
                            select.value = 'Shopping';
                        } else {
                            select.value = 'Attraction';
                        }
                    }
                } catch (error) {
                    console.error("Error fetching place details", error);
                }
            });

            dropdown.appendChild(row);
        });

        dropdown.classList.remove('hidden');
    }
}

function escapeHtml(str) {
    return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

function toggleActionBarForm(button, type) {
    const container = button.closest('.day-action-bar-container');
    container.querySelector('.form-place').classList.add('hidden');
    container.querySelector('.form-note').classList.add('hidden');
    container.querySelector('.form-checklist').classList.add('hidden');

    if (type === 'place') {
        container.querySelector('.form-place').classList.remove('hidden');
        container.querySelector('.form-place input[name="PlaceName"]').focus();
    } else if (type === 'note') {
        container.querySelector('.form-note').classList.remove('hidden');
        container.querySelector('.form-note textarea[name="NoteContent"]').focus();
    } else if (type === 'checklist') {
        container.querySelector('.form-checklist').classList.remove('hidden');
        container.querySelector('.form-checklist input[name="ChecklistTitle"]').focus();
    }
}

function cancelActionBarForm(button) {
    const container = button.closest('.day-action-bar-container');
    container.querySelector('.form-place').classList.add('hidden');
    container.querySelector('.form-note').classList.add('hidden');
    container.querySelector('.form-checklist').classList.add('hidden');
}

function submitNoteForm(button) {
    const container = button.closest('.day-action-bar-container');
    const noteArea = container.querySelector('textarea[name="NoteContent"]');
    const content = noteArea.value.trim();
    if (!content) return;

    const tripId = container.dataset.tripId;
    const tripDayId = container.dataset.dayId;

    $.ajax({
        url: '/Trip/AddNoteToDay',
        type: 'POST',
        data: { tripId: tripId, tripDayId: tripDayId, content: content },
        success: function(res) {
            if (res.success) {
                const targetTimeline = container.previousElementSibling;
                if (targetTimeline && targetTimeline.classList.contains('activities-container')) {
                    const newCard = `
                        <div class="timeline-card activity-card bg-slate-50 border border-slate-200 rounded-2xl p-5 hover:shadow-md transition-all flex justify-between items-start cursor-grab active:cursor-grabbing" draggable="true" data-timeline-id="${res.id}" data-timeline-type="Note">
                            <div class="flex items-start space-x-3 flex-1 pr-4">
                                <div class="mt-1 text-slate-300 cursor-grab active:cursor-grabbing hover:text-indigo-500 transition-colors drag-handle shrink-0">
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" d="M4 8h16M4 16h16" />
                                    </svg>
                                </div>
                                <div class="w-8 h-8 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
                                    <svg class="w-4.5 h-4.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path>
                                    </svg>
                                </div>
                                <div class="text-sm text-slate-650 leading-relaxed pt-0.5 whitespace-pre-line">
                                    ${escapeHtml(content)}
                                </div>
                            </div>
                            <button type="button" onclick="deleteTimelineItem(${res.id}, 'Note')" class="text-slate-400 hover:text-red-500 transition-colors p-1.5 rounded-lg hover:bg-slate-100" title="Delete Note">
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                </svg>
                            </button>
                        </div>
                    `;
                    targetTimeline.insertAdjacentHTML('beforeend', newCard);
                }
                noteArea.value = '';
                cancelActionBarForm(button);
            }
        },
        error: function() {
            showAlertModal('Error', 'Failed to add note.');
        }
    });
}

function submitChecklistForm(button) {
    const container = button.closest('.day-action-bar-container');
    const input = container.querySelector('input[name="ChecklistTitle"]');
    const title = input.value.trim();
    if (!title) return;

    const tripId = container.dataset.tripId;
    const tripDayId = container.dataset.dayId;

    $.ajax({
        url: '/Trip/AddChecklistToDay',
        type: 'POST',
        data: { tripId: tripId, tripDayId: tripDayId, title: title },
        success: function(res) {
            if (res.success) {
                const targetTimeline = container.previousElementSibling;
                if (targetTimeline && targetTimeline.classList.contains('activities-container')) {
                    const newCard = `
                        <div class="timeline-card activity-card bg-slate-50 border border-slate-200 rounded-2xl p-5 hover:shadow-md transition-all flex justify-between items-start cursor-grab active:cursor-grabbing" draggable="true" data-timeline-id="${res.id}" data-timeline-type="Checklist">
                            <div class="flex items-start space-x-3 flex-1 pr-4">
                                <div class="mt-1 text-slate-300 cursor-grab active:cursor-grabbing hover:text-indigo-500 transition-colors drag-handle shrink-0">
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" d="M4 8h16M4 16h16" />
                                    </svg>
                                </div>
                                <div class="w-8 h-8 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
                                    <svg class="w-4.5 h-4.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"></path>
                                    </svg>
                                </div>
                                <div class="flex-1 pt-0.5">
                                    <h4 class="font-bold text-slate-800 text-sm mb-3">${escapeHtml(title)}</h4>
                                    <ul class="space-y-2.5 mb-3 checklist-items-list" data-checklist-id="${res.id}">
                                    </ul>
                                    <div class="flex items-center space-x-2 mt-2 pt-1.5 border-t border-slate-200/50">
                                        <input type="text" placeholder="Add some items..." class="bg-white border border-slate-200 rounded-lg px-2.5 py-1 text-xs w-full max-w-[200px] focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500" onkeydown="handleAddChecklistItemKeyDown(event, ${res.id})" />
                                        <button type="button" onclick="submitNewChecklistItem(this, ${res.id})" class="text-xs font-bold text-indigo-600 hover:text-indigo-700 px-2 py-1">Add</button>
                                    </div>
                                </div>
                            </div>
                            <button type="button" onclick="deleteTimelineItem(${res.id}, 'Checklist')" class="text-slate-400 hover:text-red-500 transition-colors p-1.5 rounded-lg hover:bg-slate-100" title="Delete Checklist">
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                </svg>
                            </button>
                        </div>
                    `;
                    targetTimeline.insertAdjacentHTML('beforeend', newCard);
                }
                input.value = '';
                cancelActionBarForm(button);
            }
        },
        error: function() {
            showAlertModal('Error', 'Failed to add checklist.');
        }
    });
}

function submitNewChecklistItem(button, checklistId) {
    const container = button.closest('.timeline-card');
    const input = container.querySelector('input[placeholder="Add some items..."]');
    const content = input.value.trim();
    if (!content) return;

    $.ajax({
        url: '/Trip/AddChecklistItem',
        type: 'POST',
        data: { checklistId: checklistId, content: content },
        success: function(res) {
            if (res.success) {
                const list = container.querySelector('.checklist-items-list');
                if (list) {
                    const newItem = `
                        <li class="flex items-center text-xs text-slate-600" data-item-id="${res.id}">
                            <input type="checkbox" onchange="toggleChecklistItem(this)" class="mr-2.5 h-4 w-4 rounded text-indigo-600 focus:ring-indigo-500 border-slate-300" />
                            <span class="text-slate-700 font-medium">${escapeHtml(content)}</span>
                        </li>
                    `;
                    list.insertAdjacentHTML('beforeend', newItem);
                }
                input.value = '';
            }
        },
        error: function() {
            showAlertModal('Error', 'Failed to add checklist item.');
        }
    });
}

function handleAddChecklistItemKeyDown(event, checklistId) {
    if (event.key === 'Enter') {
        event.preventDefault();
        const input = event.target;
        const button = input.nextElementSibling;
        submitNewChecklistItem(button, checklistId);
    }
}

function toggleChecklistItem(checkbox) {
    const li = checkbox.closest('li');
    const itemId = li.dataset.itemId;
    const label = checkbox.nextElementSibling;

    $.ajax({
        url: '/Trip/ToggleChecklistItem',
        type: 'POST',
        data: { itemId: itemId },
        success: function(res) {
            if (res.success) {
                if (res.isCompleted) {
                    label.classList.add('line-through', 'text-slate-400');
                    label.classList.remove('text-slate-700', 'font-medium');
                } else {
                    label.classList.remove('line-through', 'text-slate-400');
                    label.classList.add('text-slate-700', 'font-medium');
                }
            }
        },
        error: function() {
            checkbox.checked = !checkbox.checked;
            showAlertModal('Error', 'Failed to update task.');
        }
    });
}

function deleteTimelineItem(itemId, type) {
    showConfirmModal({
        title: 'Delete ' + type,
        message: 'Are you sure you want to delete this ' + type.toLowerCase() + '?',
        danger: true,
        confirmText: 'Delete',
        onConfirm: function() {
            $.ajax({
                url: '/Trip/DeleteTimelineItem',
                type: 'POST',
                data: { itemId: itemId, type: type },
                success: function() {
                    const card = document.querySelector(`.timeline-card[data-timeline-id="${itemId}"][data-timeline-type="${type}"]`);
                    if (card) {
                        card.remove();
                    }
                },
                error: function() {
                    showAlertModal('Error', 'Failed to delete item.');
                }
            });
        }
    });
}

document.addEventListener('DOMContentLoaded', () => {
    let draggedCard = null;
    let sourceDayId = null;

    function initDragAndDrop() {
        const cards = document.querySelectorAll('.activity-card');
        const containers = document.querySelectorAll('.activities-container');

        cards.forEach(card => {
            if (card.dataset.dragInitialized) return;
            card.dataset.dragInitialized = 'true';

            card.addEventListener('dragstart', (e) => {
                draggedCard = card;
                const container = card.closest('.activities-container');
                sourceDayId = container ? container.dataset.dayId : null;
                card.classList.add('dragging');
                e.dataTransfer.setData('text/plain', card.dataset.timelineId);
                e.dataTransfer.effectAllowed = 'move';
            });

            card.addEventListener('dragend', () => {
                card.classList.remove('dragging');
                draggedCard = null;
                sourceDayId = null;
                containers.forEach(c => c.classList.remove('drag-over'));
            });
        });

        containers.forEach(container => {
            if (container.dataset.dragInitialized) return;
            container.dataset.dragInitialized = 'true';

            container.addEventListener('dragover', (e) => {
                e.preventDefault();
                e.dataTransfer.dropEffect = 'move';
                container.classList.add('drag-over');

                const afterElement = getDragAfterElement(container, e.clientY);
                if (afterElement == null) {
                    container.appendChild(draggedCard);
                } else {
                    container.insertBefore(draggedCard, afterElement);
                }
            });

            container.addEventListener('dragleave', () => {
                container.classList.remove('drag-over');
            });

            container.addEventListener('drop', async (e) => {
                e.preventDefault();
                container.classList.remove('drag-over');

                const targetDayId = container.dataset.dayId;

                // Record activity sequence before drop
                const beforeActivitySequence = Array.from(document.querySelectorAll('.timeline-card[data-timeline-type="Activity"]'))
                    .map(c => c.dataset.timelineId + '-' + (c.closest('.activities-container')?.dataset.dayId || ''))
                    .join(',');

                const items = Array.from(container.querySelectorAll('.timeline-card'))
                    .map(c => ({
                        id: parseInt(c.dataset.timelineId),
                        type: c.dataset.timelineType
                    }))
                    .filter(i => !isNaN(i.id));

                try {
                    const response = await fetch('/Trip/ReorderTimelineItems', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            tripDayId: parseInt(targetDayId),
                            items: items
                        })
                    });

                    if (response.ok) {
                        console.log('Reordered timeline successfully');
                        if (sourceDayId && sourceDayId !== targetDayId) {
                            const sourceContainer = document.querySelector(`.activities-container[data-day-id="${sourceDayId}"]`);
                            if (sourceContainer) {
                                const sourceItems = Array.from(sourceContainer.querySelectorAll('.timeline-card'))
                                    .map(c => ({
                                        id: parseInt(c.dataset.timelineId),
                                        type: c.dataset.timelineType
                                    }))
                                    .filter(i => !isNaN(i.id));

                                await fetch('/Trip/ReorderTimelineItems', {
                                    method: 'POST',
                                    headers: {
                                        'Content-Type': 'application/json'
                                    },
                                    body: JSON.stringify({
                                        tripDayId: parseInt(sourceDayId),
                                        items: sourceItems
                                    })
                                });
                            }
                        }

                        // Check activity sequence after drop to decide on reload
                        const afterActivitySequence = Array.from(document.querySelectorAll('.timeline-card[data-timeline-type="Activity"]'))
                            .map(c => c.dataset.timelineId + '-' + (c.closest('.activities-container')?.dataset.dayId || ''))
                            .join(',');

                        if (beforeActivitySequence !== afterActivitySequence) {
                            location.reload(); // Reload only if activities reordered/moved (updates map/routes)
                        } else {
                            console.log('Only notes/checklists reordered. Skipping page reload.');
                        }
                    } else {
                        console.error('Failed to reorder timeline');
                    }
                } catch (err) {
                    console.error('Error reordering timeline', err);
                }
            });
        });
    }

    function getDragAfterElement(container, y) {
        const draggableElements = [...container.querySelectorAll('.activity-card:not(.dragging)')];

        return draggableElements.reduce((closest, child) => {
            const box = child.getBoundingClientRect();
            const offset = y - box.top - box.height / 2;
            if (offset < 0 && offset > closest.offset) {
                return { offset: offset, element: child };
            } else {
                return closest;
            }
        }, { offset: Number.NEGATIVE_INFINITY }).element;
    }

    initDragAndDrop();

    const observer = new MutationObserver(initDragAndDrop);
    observer.observe(document.body, { childList: true, subtree: true });
});

// Reusable Confirmation Modal Logic
let confirmCallback = null;

function showConfirmModal(options) {
    const modal = document.getElementById('confirmation-modal');
    if (!modal) {
        if (options.onConfirm) {
            const res = confirm(options.message || 'Are you sure?');
            if (res) options.onConfirm();
        }
        return;
    }

    const titleEl = document.getElementById('confirm-modal-title');
    const msgEl = document.getElementById('confirm-modal-message');
    const confirmBtn = document.getElementById('confirm-modal-confirm');
    const cancelBtn = document.getElementById('confirm-modal-cancel');
    const iconContainer = document.getElementById('confirm-modal-icon');

    if (titleEl) titleEl.textContent = options.title || 'Are you sure?';
    if (msgEl) msgEl.textContent = options.message || 'This action cannot be undone.';
    
    if (confirmBtn) {
        confirmBtn.className = "px-4 py-2 text-white rounded-xl text-sm font-semibold shadow-sm transition-all " + 
            (options.danger ? "bg-rose-600 hover:bg-rose-700 shadow-rose-500/10" : "bg-indigo-600 hover:bg-indigo-700 shadow-indigo-500/10");
        confirmBtn.textContent = options.confirmText || 'Confirm';
    }

    if (cancelBtn) {
        if (options.hideCancel) {
            cancelBtn.classList.add('hidden');
        } else {
            cancelBtn.classList.remove('hidden');
            cancelBtn.textContent = options.cancelText || 'Cancel';
        }
    }

    if (iconContainer) {
        if (options.danger) {
            iconContainer.className = "w-10 h-10 rounded-full bg-rose-50 flex items-center justify-center text-rose-600 shrink-0";
            iconContainer.innerHTML = `<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path></svg>`;
        } else {
            iconContainer.className = "w-10 h-10 rounded-full bg-indigo-50 flex items-center justify-center text-indigo-600 shrink-0";
            iconContainer.innerHTML = `<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>`;
        }
    }

    confirmCallback = options.onConfirm;

    modal.classList.remove('hidden');

    const handleConfirm = () => {
        if (confirmCallback) confirmCallback();
        closeConfirmModal();
    };

    const handleCancel = () => {
        closeConfirmModal();
    };

    if (confirmBtn) confirmBtn.onclick = handleConfirm;
    if (cancelBtn) cancelBtn.onclick = handleCancel;
}

function closeConfirmModal() {
    const modal = document.getElementById('confirmation-modal');
    if (modal) modal.classList.add('hidden');
    confirmCallback = null;
}

function showAlertModal(title, message) {
    showConfirmModal({
        title: title,
        message: message,
        confirmText: 'OK',
        hideCancel: true,
        onConfirm: function() {}
    });
}

function confirmDeleteTrip(tripId) {
    showConfirmModal({
        title: 'Delete Trip',
        message: 'Are you sure you want to permanently delete this trip? All plans, checklists, notes, and members will be lost.',
        danger: true,
        confirmText: 'Delete',
        onConfirm: function() {
            const form = document.createElement('form');
            form.method = 'POST';
            form.action = window.TripConfig.deleteTripUrl;
            
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'tripId';
            input.value = tripId;
            form.appendChild(input);
            
            document.body.appendChild(form);
            form.submit();
        }
    });
}

function confirmRemoveMember(tripId, memberId, email) {
    showConfirmModal({
        title: 'Remove Collaborator',
        message: `Are you sure you want to remove ${email} from this trip? They will lose all access.`,
        danger: true,
        confirmText: 'Remove',
        onConfirm: function() {
            $.ajax({
                url: window.TripConfig.removeMemberUrl,
                type: 'POST',
                data: { tripId: tripId, memberId: memberId },
                success: function(res) {
                    if (res.success) {
                        location.reload();
                    } else {
                        showAlertModal('Error', res.message || 'Failed to remove member.');
                    }
                },
                error: function(xhr) {
                    showAlertModal('Error', 'Error: ' + (xhr.responseText || 'Could not complete request.'));
                }
            });
        }
    });
}

function changeTravelMode(fromActivityId, toActivityId, travelMode) {
    $.ajax({
        url: window.TripConfig.updateTravelModeUrl,
        type: 'POST',
        data: { fromActivityId: fromActivityId, toActivityId: toActivityId, travelMode: travelMode },
        success: function (res) {
            if (res.success) {
                location.reload();
            } else {
                showAlertModal('Error', res.message || 'Failed to update travel mode.');
            }
        },
        error: function (xhr) {
            showAlertModal('Error', 'Error: ' + (xhr.responseText || 'Could not complete request.'));
        }
    });
}
