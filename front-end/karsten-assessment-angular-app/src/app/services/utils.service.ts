import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class UtilsService {
  formatDateMmddyyyy(dateString: string): string {
    if (!dateString) return '';
    
    // Handle yyyy-MM-dd format explicitly to avoid timezone issues
    if (/^\d{4}-\d{2}-\d{2}/.test(dateString)) {
      const parts = dateString.split('-');
      if (parts.length >= 3) {
        const year = parseInt(parts[0], 10);
        const month = parseInt(parts[1], 10);
        const day = parseInt(parts[2], 10);
        return `${String(month).padStart(2, '0')}/${String(day).padStart(2, '0')}/${year}`;
      }
    }
    
    // For other formats, parse as Date
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return dateString;
    
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const year = date.getFullYear();
    return `${month}/${day}/${year}`;
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value);
  }
}