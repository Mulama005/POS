import { useEffect, useState } from "react";
import "./LoadingScreen.css";

interface LoadingScreenProps {
    message?: string;
}

const insights = [
    { label: "Opening readiness", title: "Preparing your point of sale", detail: "Local data is syncing so your team can get to work." },
    { label: "Product catalogue", title: "Getting products ready", detail: "Cached products are being checked for a faster checkout." },
    { label: "Offline access", title: "Your workspace is coming online", detail: "AyiyaPOS is preparing essential information for this device." },
];

export default function LoadingScreen({ message = "Loading..." }: LoadingScreenProps) {
    const [insightIndex, setInsightIndex] = useState(0);
    useEffect(() => {
        const timer = window.setInterval(() => setInsightIndex((index) => (index + 1) % insights.length), 3600);
        return () => window.clearInterval(timer);
    }, []);
    const insight = insights[insightIndex];
    return (
        <div className="loading-screen" role="status" aria-live="polite">
            <div className="loading-content">
                <div className="loading-brand">AyiyaPOS</div>
                <section className="loading-insight" key={insightIndex}>
                    <span className="loading-eyebrow">{insight.label}</span>
                    <h1>{insight.title}</h1>
                    <p>{insight.detail}</p>
                    <div className="loading-progress" aria-hidden="true"><span /></div>
                </section>
                <p className="loading-message"><span className="loading-spinner" />{message}</p>
            </div>
        </div>
    );
}
