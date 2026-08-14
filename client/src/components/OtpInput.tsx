import { useEffect, useRef, useState, type ClipboardEvent, type KeyboardEvent } from 'react'
import './OtpInput.css'

interface OtpInputProps {
  length?: number
  onComplete: (code: string) => void
  disabled?: boolean
}

export function OtpInput({ length = 6, onComplete, disabled }: OtpInputProps) {
  const [digits, setDigits] = useState<string[]>(Array(length).fill(''))
  const inputRefs = useRef<(HTMLInputElement | null)[]>([])

  useEffect(() => {
    inputRefs.current[0]?.focus()
  }, [])

  const handleChange = (index: number, value: string) => {
    const digit = value.replace(/\D/g, '').slice(-1)
    const next = [...digits]
    next[index] = digit
    setDigits(next)

    if (digit && index < length - 1) {
      inputRefs.current[index + 1]?.focus()
    }
    if (digit && index === length - 1 && next.every((d) => d !== '')) {
      onComplete(next.join(''))
    }
  }

  const handleKeyDown = (index: number, e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace') {
      if (digits[index]) {
        const next = [...digits]
        next[index] = ''
        setDigits(next)
      } else if (index > 0) {
        inputRefs.current[index - 1]?.focus()
      }
    } else if (e.key === 'ArrowLeft' && index > 0) {
      inputRefs.current[index - 1]?.focus()
    } else if (e.key === 'ArrowRight' && index < length - 1) {
      inputRefs.current[index + 1]?.focus()
    }
  }

  const handlePaste = (e: ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault()
    const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, length)
    if (!pasted) return
    const next = Array(length).fill('')
    pasted.split('').forEach((ch, i) => {
      next[i] = ch
    })
    setDigits(next)
    const lastIndex = Math.min(pasted.length, length - 1)
    inputRefs.current[lastIndex]?.focus()
    if (pasted.length === length) onComplete(pasted)
  }

  return (
    <div className="otp-input-row" role="group" aria-label="Verification code">
      {digits.map((digit, i) => (
        <input
          key={i}
          ref={(el) => {
            inputRefs.current[i] = el
          }}
          type="text"
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          maxLength={1}
          value={digit}
          disabled={disabled}
          onChange={(e) => handleChange(i, e.target.value)}
          onKeyDown={(e) => handleKeyDown(i, e)}
          onPaste={i === 0 ? handlePaste : undefined}
          className={`otp-input-cell${digit ? ' otp-input-cell--filled' : ''}`}
          aria-label={`Digit ${i + 1} of ${length}`}
        />
      ))}
    </div>
  )
}