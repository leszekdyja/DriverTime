import { useState } from "react";
import { uploadDddFile } from "./services/api";

function App() {
    const [selectedFile, setSelectedFile] =
        useState<File | null>(null);

    const [message, setMessage] =
        useState("");

    const handleUpload = async () => {
        if (!selectedFile) {
            setMessage("Wybierz plik DDD");
            return;
        }

        try {
            setMessage("Wysy³anie pliku...");

            const result =
                await uploadDddFile(selectedFile);

            console.log(result);

            setMessage("Import zakoñczony sukcesem");
        }
        catch (error) {
            console.error(error);

            setMessage("B³¹d importu pliku");
        }
    };

    return (
        <div
            style={{
                padding: "40px",
                fontFamily: "Arial"
            }}
        >
            <h1>DriverTime</h1>

            <h2>Import pliku DDD</h2>

            <input
                type="file"
                accept=".ddd"
                onChange={(e) => {
                    if (e.target.files &&
                        e.target.files.length > 0) {

                        setSelectedFile(
                            e.target.files[0]);
                    }
                }}
            />

            <br />
            <br />

            <button onClick={handleUpload}>
                Wyœlij plik
            </button>

            <p>{message}</p>
        </div>
    );
}

export default App;