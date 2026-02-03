export function getTimezoneOffset() {
    // Returns the timezone offset in minutes (e.g., -60 for UTC+1, 120 for UTC-2)
    return new Date().getTimezoneOffset();
}
