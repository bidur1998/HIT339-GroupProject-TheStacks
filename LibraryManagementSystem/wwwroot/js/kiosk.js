// Kiosk helpers (Group Project - Public Self-Service Kiosk).
// 1. On-screen keypad: buttons with data-key type into the input named by
//    the keypad's data-target attribute.
// 2. Idle timer: on a shared public screen, a patron who walks away must
//    not stay signed in. After the configured idle time a warning shows,
//    then the browser goes to /Kiosk/Timeout which clears the session.
//    The server also enforces the timeout itself (KioskController), so
//    this script is a convenience, not the security control.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.keypad').forEach(function (pad) {
        var input = document.getElementById(pad.getAttribute('data-target'));
        if (!input) return;

        pad.addEventListener('click', function (e) {
            var button = e.target.closest('button[data-key]');
            if (!button) return;
            e.preventDefault();

            var key = button.getAttribute('data-key');
            if (key === 'back') {
                input.value = input.value.slice(0, -1);
            } else if (key === 'clear') {
                input.value = '';
            } else if (key === 'enter') {
                if (input.form) input.form.requestSubmit();
            } else if (!input.maxLength || input.maxLength < 0 || input.value.length < input.maxLength) {
                input.value += key;
            }
            input.focus();
        });
    });

    var body = document.body;
    if (body.getAttribute('data-signed-in') !== 'true') return;

    var idleSeconds = parseInt(body.getAttribute('data-idle-seconds'), 10) || 120;
    var warnSeconds = Math.min(15, Math.floor(idleSeconds / 2));
    var timeoutUrl = body.getAttribute('data-timeout-url');
    var warning = document.getElementById('idle-warning');
    var countLabel = document.getElementById('idle-count');
    var remaining = idleSeconds;

    function reset() {
        remaining = idleSeconds;
        if (warning) warning.hidden = true;
    }

    ['click', 'touchstart', 'keydown'].forEach(function (evt) {
        document.addEventListener(evt, reset, { passive: true });
    });

    setInterval(function () {
        remaining--;
        if (remaining <= warnSeconds && warning) {
            warning.hidden = false;
            if (countLabel) countLabel.textContent = Math.max(remaining, 0);
        }
        if (remaining <= 0) {
            window.location.href = timeoutUrl;
        }
    }, 1000);
});
