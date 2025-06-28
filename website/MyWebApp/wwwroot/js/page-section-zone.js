window.addEventListener('load', () => {
    const pageSelect = document.querySelector('select[name="PageId"]');
    const zoneSelect = document.getElementById('zone-select');
    if (!pageSelect || !zoneSelect) return;

    function loadZones() {
        const id = pageSelect.value;
        if (!id) { zoneSelect.innerHTML = ''; return; }
        fetch(`/Api/GetZonesForPage/${id}`)
            .then(r => r.json())
            .then(list => {
                zoneSelect.innerHTML = list.map(a => `<option value="${a}">${a}</option>`).join('');
                if (zoneSelect.dataset.selected)
                    zoneSelect.value = zoneSelect.dataset.selected;
            });
    }
    loadZones();
    pageSelect.addEventListener('change', loadZones);
});
