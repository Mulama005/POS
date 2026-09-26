export function downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
    // Keep the object URL alive long enough for the browser to begin saving it.
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export function downloadCsvFile(filename: string, rows: Array<Array<string | number | null>>) {
    const csvCell = (value: string | number | null) => `"${String(value ?? "").replaceAll('"', '""')}"`;
    const blob = new Blob([rows.map(row => row.map(csvCell).join(",")).join("\n")], { type: "text/csv;charset=utf-8" });
    downloadBlob(blob, filename);
}
