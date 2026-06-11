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
            row.className = 'px-4 py-2.5 text-sm text-slate-700 hover:bg-indigo-50 cursor-pointer transition-colors';
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

            const placeId = document.getElementById("editTripPlaceId").value || "@(Model.PlacesToVisit?.FirstOrDefault()?.GooglePlaceId)";
            const startDate = document.getElementById("editTripStartDate").value;
            const endDate = document.getElementById("editTripEndDate").value;
            const description = document.getElementById("editTripDescription").value;
            const locationText = document.getElementById("editTripLocationInput").value;

            if (!locationText.trim()) {
                alert("Please select a location.");
                return;
            }

            const tripId = @Model.TripId;

            if (tripId === 0) {
                // Unsaved trip: redirect to re-render client-side GET view with new parameters
                window.location.href = `/Trip?placeId=${encodeURIComponent(placeId)}&startDate=${encodeURIComponent(startDate)}&endDate=${encodeURIComponent(endDate)}`;
            } else {
                // Saved trip: mock behavior for now (UI changes logged, exit edit mode)
                console.log("Mock Save Trip details:", {
                    tripId: tripId,
                    title: locationText,
                    description: description,
                    startDate: startDate,
                    endDate: endDate,
                    placeId: placeId,
                    latitude: document.getElementById("editTripLat").value,
                    longitude: document.getElementById("editTripLng").value,
                    address: document.getElementById("editTripAddress").value,
                    placeName: document.getElementById("editTripPlaceName").value
                });
                $.ajax({
                    url: "@Url.Action("UpdateTripDetails", "Trip")",
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
                        // fires on 200–299
                        location.reload();
                        console.log('Success:', xhr.status); // 200
                    },
                    error: function (xhr, textStatus, errorThrown) {
                        // fires on 4xx / 5xx
                        switch (xhr.status) {
                            case 400:
                                alert('Bad request');
                                break;
                            case 401:
                                alert('Unauthorized');
                                break;
                            case 404:
                                alert('Not found');
                                break;
                            case 500:
                                alert('Server error');
                                break;
                            default:
                                alert('Error: ' + xhr.status);
                        }
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
        url: "@Url.Action("SetBudget", "Trip")",
        type: "POST",
        data: { tripId: tripId, budget: budget },
        success: function (data, textStatus, xhr) {
            // fires on 200–299
            console.log('Success:', xhr.status); // 200
        },
        error: function (xhr, textStatus, errorThrown) {
            // fires on 4xx / 5xx
            switch (xhr.status) {
                case 400:
                    alert('Bad request');
                    break;
                case 401:
                    alert('Unauthorized');
                    break;
                case 404:
                    alert('Not found');
                    break;
                case 500:
                    alert('Server error');
                    break;
                default:
                    alert('Error: ' + xhr.status);
            }
        }
    });
}

let map;
const lat = @(Model.Latitude?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) ?? "47.200");
const lng = @(Model.Longitude?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) ?? "13.200");
const hasCoords = @(Model.Latitude.HasValue && Model.Longitude.HasValue ? "true" : "false");

const places = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(
    (Model.PlacesToVisit ?? new List < PlaceViewModel > ())
        .Concat((Model.Days ?? new List < TripDayViewModel > ()).SelectMany(d => d.Activities ?? new List < TripActivityViewModel > ()).Where(a => a.Place != null).Select(a => a.Place))
        .Where(p => p != null && p.Latitude.HasValue && p.Longitude.HasValue)
        .Select(p => new
            {
                name = p.Name,
                lat = (double)p.Latitude.Value,
                lng = (double)p.Longitude.Value,
                category = p.PrimaryCategory
            })
));

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
