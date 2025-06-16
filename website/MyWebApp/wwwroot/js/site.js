// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

window.addEventListener('load', function () {
    const timeoutMinutes = parseInt(document.body.dataset.sessionTimeout || '0');
    if (timeoutMinutes > 0) {
        const warnTime = (timeoutMinutes - 5) * 60 * 1000;
        setTimeout(function () {
            if (confirm('Session expiring soon. Stay signed in?')) {
                fetch('/');
            }
        }, warnTime);
    }
});
