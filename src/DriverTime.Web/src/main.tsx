import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import App from "./App.tsx";
import "./index.css";
import "./styles/theme.css";
import "./styles/ui.css";
import "./styles/saas-demo.css";

createRoot(document.getElementById("root")!).render(
    <StrictMode>
        <App />
    </StrictMode>,
);
