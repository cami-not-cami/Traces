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
                alert("Please select a location.");
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
                        alert('Error updating trip: ' + xhr.status);
                    }
                });
                exitBadgeEditMode();
            }
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
            alert('Error saving budget: ' + xhr.status);
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
            infowindow.open(map, marker);
        });

        bounds.extend(marker.getPosition());
        hasMarkers = true;
    });

    if (hasMarkers && places.length > 1) {
        map.fitBounds(bounds);
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

function showInlineForm(button) {
    // Hide trigger button, show form
    const container = button.closest('.inline-add-container');
    button.classList.add('hidden');
    const form = container.querySelector('.inline-add-form');
    form.classList.remove('hidden');
    // Focus the autocomplete input
    const input = form.querySelector('.day-autocomplete');
    if (input) input.focus();
}

function hideInlineForm(button) {
    // Hide form, show trigger button
    const container = button.closest('.inline-add-container');
    const form = container.querySelector('.inline-add-form');
    form.classList.add('hidden');
    const trigger = container.querySelector('button');
    trigger.classList.remove('hidden');
}

// Drag & Drop logic for activities reordering
document.addEventListener('DOMContentLoaded', () => {
    let draggedCard = null;
    let sourceDayId = null;

    function initDragAndDrop() {
        const cards = document.querySelectorAll('.activity-card');
        const containers = document.querySelectorAll('.activities-container');

        cards.forEach(card => {
            // Ensure events are attached only once
            if (card.dataset.dragInitialized) return;
            card.dataset.dragInitialized = 'true';

            card.addEventListener('dragstart', (e) => {
                draggedCard = card;
                const container = card.closest('.activities-container');
                sourceDayId = container ? container.dataset.dayId : null;
                card.classList.add('dragging');
                e.dataTransfer.setData('text/plain', card.dataset.activityId);
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
                const activityId = e.dataTransfer.getData('text/plain');

                // Extract all activity IDs in target container in their new order
                const activityIds = Array.from(container.querySelectorAll('.activity-card'))
                    .map(c => parseInt(c.dataset.activityId));

                // Send the update to the server
                try {
                    const response = await fetch('/Trip/ReorderActivities', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            tripDayId: parseInt(targetDayId),
                            activityIds: activityIds
                        })
                    });

                    if (response.ok) {
                        console.log('Reordered successfully');
                        // If it was moved between days, also trigger reordering update on the source container to normalize it.
                        if (sourceDayId && sourceDayId !== targetDayId) {
                            const sourceContainer = document.querySelector(`.activities-container[data-day-id="${sourceDayId}"]`);
                            if (sourceContainer) {
                                const sourceActivityIds = Array.from(sourceContainer.querySelectorAll('.activity-card'))
                                    .map(c => parseInt(c.dataset.activityId));

                                await fetch('/Trip/ReorderActivities', {
                                    method: 'POST',
                                    headers: {
                                        'Content-Type': 'application/json'
                                    },
                                    body: JSON.stringify({
                                        tripDayId: parseInt(sourceDayId),
                                        activityIds: sourceActivityIds
                                    })
                                });
                            }
                        }
                    } else {
                        console.error('Failed to reorder activities');
                    }
                } catch (err) {
                    console.error('Error reordering activities', err);
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

    // Re-initialize drag and drop when new elements are added dynamically
    const observer = new MutationObserver(initDragAndDrop);
    observer.observe(document.body, { childList: true, subtree: true });
});
