// Toggles which "extra info" fields are shown on the item Create/Edit form
// based on the selected Item Type. Purely a display convenience - the
// server re-validates whichever fields matter for the chosen type
// regardless of what JavaScript does on the client.
document.addEventListener('DOMContentLoaded', function () {
    var typeSelect = document.getElementById('ItemType');
    if (!typeSelect) return;

    var sections = {
        Book: document.getElementById('fields-book'),
        Toy: document.getElementById('fields-toy'),
        Music: document.getElementById('fields-music')
    };

    function applyVisibility() {
        var selected = typeSelect.value;
        Object.keys(sections).forEach(function (key) {
            var section = sections[key];
            if (!section) return;
            section.style.display = key === selected ? '' : 'none';
        });
    }

    typeSelect.addEventListener('change', applyVisibility);
    applyVisibility();
});
