const API_URL = import.meta.env.VITE_API_URL;

export async function uploadDddFile(file: File) {
    const formData = new FormData();

    formData.append("file", file);

    const response = await fetch(
        `${API_URL}/api/ddd-files/upload`,
        {
            method: "POST",
            body: formData,
        });

    if (!response.ok) {
        throw new Error("Upload failed");
    }

    return response.json();
}

export async function getDddFiles() {
    const response = await fetch(
        `${API_URL}/api/ddd-files`);

    if (!response.ok) {
        throw new Error("Failed to fetch files");
    }

    return response.json();
}