import "./LoadingScreen.css";

interface LoadingScreenProps {
    message?: string;
}

export default function LoadingScreen({ message = "Loading..." }: LoadingScreenProps) {
    return (
        <div className="loading-screen">
            <div className="loading-content">
                <div className="loading-spinner" />
                <p className="loading-message">{message}</p>
            </div>
        </div>
    );
}